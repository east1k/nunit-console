﻿// Copyright (c) Charlie Poole, Rob Prouse and Contributors. MIT License - see LICENSE.txt

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Schema;
using System.Xml;
using System.Xml.Serialization;

namespace NUnit.Engine
{
    /// <summary>
    /// TestPackage holds information about a set of test files to
    /// be loaded by a TestRunner. Each TestPackage represents
    /// tests for one or more test files. TestPackages may be named
    /// or anonymous, depending on the constructor used.
    /// 
    /// Upon construction, a package is given an ID (string), which
    /// remains unchanged for the lifetime of the TestPackage instance.
    /// The package ID is passed to the test framework for use in generating
    /// test IDs.
    /// 
    /// A runner that reloads test assemblies and wants the ids to remain stable
    /// should avoid creating a new package but should instead use the original
    /// package, changing settings as needed. This gives the best chance for the
    /// tests in the reloaded assembly to match those originally loaded.
    /// </summary>
    [Serializable]
    public class TestPackage : IXmlSerializable
    {
        /// <summary>
        /// Construct a named TestPackage, specifying a file path for
        /// the assembly or project to be used.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        public TestPackage(string filePath)
        {
            ID = GetNextID();

            if (filePath != null)
            {
                FullName = Path.GetFullPath(filePath);
            }
        }

        /// <summary>
        /// Construct an anonymous TestPackage that wraps test files.
        /// </summary>
        /// <param name="testFiles"></param>
        public TestPackage(IList<string> testFiles)
        {
            ID = GetNextID();

            foreach (string testFile in testFiles)
                SubPackages.Add(new TestPackage(testFile));
        }

        /// <summary>
        ///  Construct an empty TestPackage.
        /// </summary>
        public TestPackage() { }

        private static int _nextID = 0;

        private static string GetNextID()
        {
            return (_nextID++).ToString();
        }

        /// <summary>
        /// Every test package gets a unique ID used to prefix test IDs within that package.
        /// </summary>
        /// <remarks>
        /// The generated ID is only unique for packages created within the same application domain.
        /// For that reason, NUnit pre-creates all test packages that will be needed.
        /// </remarks>
        public string ID { get; private set; }

        /// <summary>
        /// Gets the name of the package
        /// </summary>
        public string Name => FullName == null ? null : Path.GetFileName(FullName);

        /// <summary>
        /// Gets the path to the file containing tests. It may be
        /// an assembly or a recognized project type.
        /// </summary>
        public string FullName { get; private set; }

        /// <summary>
        /// Gets the list of SubPackages contained in this package
        /// </summary>
        public IList<TestPackage> SubPackages { get; } = new List<TestPackage>();

        /// <summary>
        /// Gets the settings dictionary for this package.
        /// </summary>
        public IDictionary<string,object> Settings { get; } = new Dictionary<string,object>();

        /// <summary>
        /// Add a subproject to the package.
        /// </summary>
        /// <param name="subPackage">The subpackage to be added</param>
        public void AddSubPackage(TestPackage subPackage)
        {
            SubPackages.Add(subPackage);

            foreach (var key in Settings.Keys)
                subPackage.Settings[key] = Settings[key];
        }

        /// <summary>
        /// Add a setting to a package and all of its subpackages.
        /// </summary>
        /// <param name="name">The name of the setting</param>
        /// <param name="value">The value of the setting</param>
        /// <remarks>
        /// Once a package is created, subpackages may have been created
        /// as well. If you add a setting directly to the Settings dictionary
        /// of the package, the subpackages are not updated. This method is
        /// used when the settings are intended to be reflected to all the
        /// subpackages under the package.
        /// </remarks>
        public void AddSetting(string name, object value)
        {
            Settings[name] = value;
            foreach (var subPackage in SubPackages)
                subPackage.AddSetting(name, value);
        }

        /// <summary>
        /// Return the value of a setting or a default.
        /// </summary>
        /// <param name="name">The name of the setting</param>
        /// <param name="defaultSetting">The default value</param>
        /// <returns></returns>
        public T GetSetting<T>(string name, T defaultSetting)
        {
            return Settings.ContainsKey(name)
                ? (T)Settings[name]
                : defaultSetting;
        }

        #region IXmlSerializable Implementation

        /// <inheritdoc />
        public XmlSchema GetSchema()
        {
            return null;
        }

        /// <inheritdoc />
        public void ReadXml(XmlReader xmlReader)
        {
            ID = xmlReader.GetAttribute("id");
            FullName = xmlReader.GetAttribute("fullname");
            if (!xmlReader.IsEmptyElement)
            {
                while (xmlReader.Read())
                {
                    switch (xmlReader.NodeType)
                    {
                        case XmlNodeType.Element:
                            switch(xmlReader.Name)
                            {
                                case "Settings":
                                    // We don't use AddSettings, which copies settings downward.
                                    // Instead, each package handles it's own settings.
                                    while (xmlReader.MoveToNextAttribute())
                                        Settings.Add(xmlReader.Name, xmlReader.Value);
                                    xmlReader.MoveToElement();
                                    break;

                                case "TestPackage":
                                    TestPackage subPackage = new TestPackage();
                                    subPackage.ReadXml(xmlReader);
                                    SubPackages.Add(subPackage);
                                    break;
                            }
                            break;

                        case XmlNodeType.EndElement:
                            if (xmlReader.Name == "TestPackage")
                                return;
                            break;

                        default:
                            throw new Exception("Unexpected EndElement: " + xmlReader.Name);
                    }
                }

                throw new Exception("Invalid XML: TestPackage Element not terminated.");
            }
        }

        /// <inheritdoc />
        public void WriteXml(XmlWriter xmlWriter)
        {
            // Write ID and FullName
            xmlWriter.WriteAttributeString("id", ID);
            if (FullName != null)
                xmlWriter.WriteAttributeString("fullname", FullName);

            // Write Settings
            if (Settings.Count != 0)
            {
                xmlWriter.WriteStartElement("Settings");

                foreach (KeyValuePair<string, object> setting in Settings)
                    xmlWriter.WriteAttributeString(setting.Key, setting.Value.ToString());

                xmlWriter.WriteEndElement();
            }

            // Write any SubPackages recursively
            foreach (TestPackage subPackage in SubPackages)
            {
                xmlWriter.WriteStartElement("TestPackage");
                subPackage.WriteXml(xmlWriter);
                xmlWriter.WriteEndElement();
            }
        }
    }

    #endregion
}
