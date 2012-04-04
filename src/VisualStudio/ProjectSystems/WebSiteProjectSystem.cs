using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using EnvDTE;
using NuGet.VisualStudio.Resources;

namespace NuGet.VisualStudio
{
    public class WebSiteProjectSystem : WebProjectSystem, IBatchProcessor<string>
    {
        private const string RootNamespace = "RootNamespace";
        private const string DefaultNamespace = "ASP";
        private const string AppCodeFolder = "App_Code";
        private const string GeneratedFilesFolder = "Generated___Files";

        private readonly HashSet<string> _excludedCodeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] _sourceFileExtensions = new[] { ".cs", ".vb" };

        public WebSiteProjectSystem(Project project, IFileSystemProvider fileSystemProvider)
            : base(project, fileSystemProvider)
        {
        }

        public override string ProjectName
        {
            get
            {
                string path = Project.GetFullPath();
                return Path.GetFileName(path);
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to catch all exceptions")]
        public override void AddReference(string referencePath, Stream stream)
        {
            string name = Path.GetFileNameWithoutExtension(referencePath);
            try
            {
                // Add a reference to the project
                this.CreateRefreshFile(PathUtility.GetAbsolutePath(Root, referencePath));

                Logger.Log(MessageLevel.Debug, VsResources.Debug_AddReference, name, ProjectName);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, VsResources.FailedToAddReference, name), e);
            }
        }

        public override void RemoveReference(string name)
        {
            // Remove the reference from the project
            var refreshFilePath = Path.Combine("bin", Path.GetFileName(name) + ".refresh");
                
            if (FileExists(refreshFilePath))
            {
                try
                {
                    DeleteFile(refreshFilePath);
                }
                catch (Exception e)
                {
                    Logger.Log(MessageLevel.Warning, e.Message);
                }
            }
            else
            {
                // If a refresh file does not exist, it might be a reference added via an earlier version of NuGet. Use the base's code path to try and 
                // remove the reference via DTE.
                base.RemoveReference(name);
            }
        }

        public override string ResolvePath(string path)
        {
            // If we're adding a source file that isn't already in the app code folder then add App_Code to the path
            if (RequiresAppCodeRemapping(path))
            {
                path = Path.Combine(AppCodeFolder, path);
            }

            return base.ResolvePath(path);
        }

        public override IEnumerable<string> GetDirectories(string path)
        {
            if (IsUnderAppCode(path))
            {
                // There is an invisible folder called Generated___Files under app code that we want to exclude from our search
                return base.GetDirectories(path).Except(new[] { GeneratedFilesFolder }, StringComparer.OrdinalIgnoreCase);
            }
            return base.GetDirectories(path);
        }

        protected override void AddGacReference(string name)
        {
            Project.Object.References.AddFromGAC(name);
        }

        public override dynamic GetPropertyValue(string propertyName)
        {
            if (propertyName.Equals(RootNamespace, StringComparison.OrdinalIgnoreCase))
            {
                return DefaultNamespace;
            }
            return base.GetPropertyValue(propertyName);
        }

        protected override bool ExcludeFile(string path)
        {
            // Exclude nothing from website projects
            return false;
        }

        public void BeginProcessing(IEnumerable<string> batch, PackageAction action)
        {
            var files = batch.OrderBy(path => path)
                             .ToList();

            foreach (var path1 in files)
            {
                foreach (var path2 in files)
                {
                    if (path1.Equals(path2, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (path1.StartsWith(path2, StringComparison.OrdinalIgnoreCase) &&
                        IsSourceFile(path1))
                    {
                        _excludedCodeFiles.Add(path1);
                    }
                }
            }
        }

        public void EndProcessing()
        {
            _excludedCodeFiles.Clear();
        }

        /// <summary>
        /// Determines if we need a source file to be under the App_Code folder
        /// </summary>
        private bool RequiresAppCodeRemapping(string path)
        {
            return !_excludedCodeFiles.Contains(path) && !IsUnderAppCode(path) && IsSourceFile(path);
        }

        private static bool IsUnderAppCode(string path)
        {
            return PathUtility.EnsureTrailingSlash(path).StartsWith(AppCodeFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSourceFile(string path)
        {
            string extension = Path.GetExtension(path);
            return _sourceFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }
    }
}