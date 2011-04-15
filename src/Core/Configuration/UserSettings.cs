﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Internal.Web.Utils;
using NuGet.Resources;
using System.Globalization;

namespace NuGet {
    public class UserSettings : ISettings {
        private XDocument _config;
        private string _configLocation;
        private readonly IFileSystem _fileSystem;

        public UserSettings(IFileSystem fileSystem) {
            if (fileSystem == null) {
                throw new ArgumentNullException("fileSystem");
            }
            _fileSystem = fileSystem;
            _configLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NuGet", "NuGet.Config");
            _config = XmlUtility.GetOrCreateDocument("configuration", _fileSystem, _configLocation);
        }

        public string GetValue(string section, string key) {
            if (String.IsNullOrEmpty(section)) {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }

            if (String.IsNullOrEmpty(key)) {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "key");
            }

            try {
                // Get the section and return null if it doesnt exist
                var sectionElement = _config.Root.Element(section);
                if (sectionElement == null) {
                    return null;
                }

                // Get the add element that matches the key and return null if it doesnt exist
                var element = sectionElement.Elements("add").Where(s => s.GetOptionalAttributeValue("key") == key).FirstOrDefault();
                if (element == null) {
                    return null;
                }

                // Return the optional value which if not there will be null;
                return element.GetOptionalAttributeValue("value");
            }
            catch (Exception e) {
                throw new InvalidOperationException(NuGetResources.UserSettings_UnableToParseConfigFile, e);
            }

        }

        public ICollection<KeyValuePair<string, string>> GetValues(string section) {
            if (String.IsNullOrEmpty(section)) {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }

            try {
                var sectionElement = _config.Root.Element(section);
                if (sectionElement == null) {
                    return null;
                }

                var kvps = new List<KeyValuePair<string, string>>();
                foreach (var e in sectionElement.Elements("add")) {
                    var key = e.GetOptionalAttributeValue("key");
                    var value = e.GetOptionalAttributeValue("value");
                    if (!String.IsNullOrEmpty(key) && value != null) {
                        kvps.Add(new KeyValuePair<string, string>(key, value));
                    }
                }

                

                return kvps.AsReadOnly();
            }
            catch (Exception e) {
                throw new InvalidOperationException(NuGetResources.UserSettings_UnableToParseConfigFile, e);
            }
        }

        public void SetValue(string section, string key, string value) {
            SetValueInternal(section, key, value);
            Save(_config);
        }

        public void SetValues(string section, ICollection<KeyValuePair<string, string>> values) {
            if (values == null) {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "values");
            }

            foreach (var kvp in values) {
                SetValueInternal(section, kvp.Key, kvp.Value);
            }
            Save(_config);
        }

        private void SetValueInternal(string section, string key, string value) {
            if (String.IsNullOrEmpty(section)) {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }
            if (String.IsNullOrEmpty(key)) {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "key");
            }
            if (value == null) {
                throw new ArgumentNullException("value");
            }

            var sectionElement = _config.Root.Element(section);
            if (sectionElement == null) {
                sectionElement = new XElement(section);
                _config.Root.Add(sectionElement);
            }

            foreach (var e in sectionElement.Elements("add")) {
                var tempKey = e.GetOptionalAttributeValue("key");

                if (tempKey == key) {
                    e.SetAttributeValue("value", value);
                    Save(_config);
                    return;
                }
            }

            var addElement = new XElement("add");
            addElement.SetAttributeValue("key", key);
            addElement.SetAttributeValue("value", value);
            sectionElement.Add(addElement);
        }

        public bool DeleteValue(string section, string key) {
            if (String.IsNullOrEmpty(section)) {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }
            if (String.IsNullOrEmpty(key)) {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "key");
            }

            var sectionElement = _config.Root.Element(section);
            if (sectionElement == null) {
                return false;
            }

            XElement elementToDelete = null;
            foreach (var e in sectionElement.Elements("add")) {
                if (e.GetOptionalAttributeValue("key") == key) {
                    elementToDelete = e;
                    break;
                }
            }
            if (elementToDelete == null) {
                return false;
            }

            elementToDelete.Remove();
            Save(_config);
            return true;

        }

        public bool DeleteSection(string section) {
            if (String.IsNullOrEmpty(section)) {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "section");
            }

            var sectionElement = _config.Root.Element(section);
            if (sectionElement == null) {
                return false;
            }

            sectionElement.Remove();
            Save(_config);
            return true;
        }

        private void Save(XDocument document) {
            _fileSystem.AddFile(_configLocation, document.Save);
        }

    }
}
