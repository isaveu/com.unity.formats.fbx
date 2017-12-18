using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using FbxExporters.EditorTools;
using NUnit.Framework;

namespace FbxExporters.UnitTests
{
    public class FbxExportSettingsTest : ExporterTestBase{
        ExportSettings m_originalSettings;

        // We read two private fields for the test.
        static System.Reflection.FieldInfo s_InstanceField; // static
        static System.Reflection.FieldInfo s_SavePathField; // member

        static FbxExportSettingsTest() {
            // all names, private or public, instance or static
            var privates = System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Instance;
            var t = typeof(ExportSettings);

            s_SavePathField = t.GetField("convertToModelSavePath", privates);
            Assert.IsNotNull(s_SavePathField, "convertToModelSavePath");

            // static fields can't be found through inheritance with GetField.
            // if we change the inheritance diagram, we have to change t.BaseType here.
            s_InstanceField = t.BaseType.GetField("s_Instance", privates);
            Assert.IsNotNull(s_InstanceField, "s_Instance");
        }

        [NUnit.Framework.SetUp]
        public void SetUp()
        {
            var settings = (ExportSettings)s_InstanceField.GetValue(null);
            m_originalSettings = settings;

            // Clear out the current instance and create a new one (but keep the original around).
            s_InstanceField.SetValue(null, null);
            s_InstanceField.SetValue(null, ScriptableObject.CreateInstance<ExportSettings>());
        }

        [NUnit.Framework.TearDown]
        public void TearDown()
        {
            // Destroy the test settings and restore the original.
            // The original might be null -- not a problem.
            var settings = (ExportSettings)s_InstanceField.GetValue(null);
            ScriptableObject.DestroyImmediate(settings);

            s_InstanceField.SetValue(null, m_originalSettings);
        }


        [Test]
        public void TestNormalizePath()
        {
            // Test slashes in both directions, and leading and trailing slashes.
            var path = "/a\\b/c/\\";
            var norm = ExportSettings.NormalizePath(path, isRelative: true);
            Assert.AreEqual("a/b/c", norm);
            norm = ExportSettings.NormalizePath(path, isRelative: false);
            Assert.AreEqual("/a/b/c", norm);

            // Test empty path. Not actually absolute, so it's treated as a relative path.
            path = "";
            norm = ExportSettings.NormalizePath(path, isRelative: true);
            Assert.AreEqual(".", norm);
            norm = ExportSettings.NormalizePath(path, isRelative: false);
            Assert.AreEqual(".", norm);

            // Test just a bunch of slashes. Root or . depending on whether it's abs or rel.
            path = "///";
            norm = ExportSettings.NormalizePath(path, isRelative: true);
            Assert.AreEqual(".", norm);
            norm = ExportSettings.NormalizePath(path, isRelative: false);
            Assert.AreEqual("/", norm);

            // Test handling of .
            path = "/a/./b/././c/.";
            norm = ExportSettings.NormalizePath(path, isRelative: true);
            Assert.AreEqual("a/b/c", norm);

            // Test handling of leading ..
            path = "..";
            norm = ExportSettings.NormalizePath(path, isRelative: true);
            Assert.AreEqual("..", norm);

            path = "../a";
            norm = ExportSettings.NormalizePath(path, isRelative: true);
            Assert.AreEqual("../a", norm);

            // Test two leading ..
            path = "../../a";
            norm = ExportSettings.NormalizePath(path, isRelative: true);
            Assert.AreEqual("../../a", norm);

            // Test .. in the middle and effect on leading /
            path = "/a/../b";
            norm = ExportSettings.NormalizePath(path, isRelative: true);
            Assert.AreEqual("b", norm);
            norm = ExportSettings.NormalizePath(path, isRelative: false);
            Assert.AreEqual("/b", norm);

            // Test that we can change the separator
            norm = ExportSettings.NormalizePath(path, isRelative: false, separator: '\\');
            Assert.AreEqual("\\b", norm);
        }

        [Test]
        public void TestGetRelativePath()
        {
            var from = "//a/b/c";
            var to = "///a/b/c/d/e";
            var relative = ExportSettings.GetRelativePath(from, to);
            Assert.AreEqual("d/e", relative);

            from = "///a/b/c/";
            to = "///a/b/c/d/e/";
            relative = ExportSettings.GetRelativePath(from, to);
            Assert.AreEqual("d/e", relative);

            from = "///aa/bb/cc/dd/ee";
            to = "///aa/bb/cc";
            relative = ExportSettings.GetRelativePath(from, to);
            Assert.AreEqual("../..", relative);

            from = "///a/b/c/d/e/";
            to = "///a/b/c/";
            relative = ExportSettings.GetRelativePath(from, to);
            Assert.AreEqual("../..", relative);

            from = "///a/b/c/d/e/";
            to = "///a/b/c/";
            relative = ExportSettings.GetRelativePath(from, to, separator: ':');
            Assert.AreEqual("..:..", relative);

            from = Path.Combine(Application.dataPath, "foo");
            to = Application.dataPath;
            relative = ExportSettings.GetRelativePath(from, to);
            Assert.AreEqual("..", relative);

            to = Path.Combine(Application.dataPath, "foo");
            relative = ExportSettings.ConvertToAssetRelativePath(to);
            Assert.AreEqual("foo", relative);

            relative = ExportSettings.ConvertToAssetRelativePath("/path/to/somewhere/else");
            Assert.AreEqual("", relative);

            relative = ExportSettings.ConvertToAssetRelativePath("/path/to/somewhere/else", requireSubdirectory: false);
            Assert.IsTrue(relative.StartsWith("../"));
            Assert.IsFalse(relative.Contains("\\"));
        }

        [Test]
        public void TestGetSetFields()
        {
            var defaultRelativePath = ExportSettings.GetRelativeSavePath();
            Assert.AreEqual(ExportSettings.kDefaultSavePath, defaultRelativePath);

            // the path to Assets but with platform-dependent separators
            var appDataPath = Application.dataPath.Replace(Path.AltDirectorySeparatorChar,
                    Path.DirectorySeparatorChar);

            var defaultAbsolutePath = ExportSettings.GetAbsoluteSavePath();
            var dataPath = Path.GetFullPath(Path.Combine(appDataPath, ExportSettings.kDefaultSavePath));
            Assert.AreEqual(dataPath, defaultAbsolutePath);

            // set; check that the saved value is platform-independent,
            // that the relative path uses / like in unity,
            // and that the absolute path is platform-specific
            ExportSettings.SetRelativeSavePath("/a\\b/c/\\");
            var convertToModelSavePath = s_SavePathField.GetValue(ExportSettings.instance);
            Assert.AreEqual("a/b/c", convertToModelSavePath);
            Assert.AreEqual("a/b/c", ExportSettings.GetRelativeSavePath());
            var platformPath = Path.Combine("a", Path.Combine("b", "c"));
            Assert.AreEqual(Path.Combine(appDataPath, platformPath), ExportSettings.GetAbsoluteSavePath());
        }

        [Test]
        public void TestFindPreferredProgram()
        {
            //Add a number of fake programs to the list, including some garbage ones
            List<string> testList = new List<string>();
            testList.Add(null);
            testList.Add(ExportSettings.GetUniqueDCCOptionName(ExportSettings.kMaxOptionName + "2000"));
            testList.Add(ExportSettings.GetUniqueDCCOptionName(ExportSettings.kMayaOptionName + "2016"));
            testList.Add(ExportSettings.GetUniqueDCCOptionName(ExportSettings.kMayaOptionName + "2017"));
            testList.Add(ExportSettings.GetUniqueDCCOptionName(ExportSettings.kMaxOptionName + "2017"));
            testList.Add(ExportSettings.GetUniqueDCCOptionName(""));
            testList.Add(ExportSettings.GetUniqueDCCOptionName(null));
            testList.Add(ExportSettings.GetUniqueDCCOptionName(ExportSettings.kMayaLtOptionName));
            testList.Add(ExportSettings.GetUniqueDCCOptionName(ExportSettings.kMayaOptionName + "2017"));

            ExportSettings.instance.SetDCCOptionNames(testList);

            int preferred = ExportSettings.instance.GetPreferredDCCApp();
            //While Maya 2017 and 3ds Max 2017 are tied for most recent, Maya 2017 (index 8) should win because we prefer Maya.
            Assert.AreEqual(preferred, 8);

            ExportSettings.instance.ClearDCCOptionNames();
            //Try running it with an empty list
            preferred = ExportSettings.instance.GetPreferredDCCApp();

            Assert.AreEqual(preferred, -1);

            ExportSettings.instance.SetDCCOptionNames(null);
            //Try running it with a null list
            preferred = ExportSettings.instance.GetPreferredDCCApp();

            Assert.AreEqual(preferred, -1);
        }

        [Test]
        public void FindDCCInstallsTest1()
        {
            string rootDir1 = GetRandomFileNamePath(extName: "");
            string rootDir2 = GetRandomFileNamePath(extName: "");

            var data = new List<Dictionary<string, List<string>>>()
             {
                #region valid test case 1 data (unique locations, one in vendor, one for maya_loc)
                new Dictionary<string,List<string>>()
                {
                    {"VENDOR_INSTALLS", new List<string>(){ rootDir1 + "/Maya2017/bin/maya.exe", rootDir2 + "/Maya2018/bin/maya.exe"} },
                    {"VENDOR_LOCATIONS", new List<string>(){ rootDir1 } },
                    {"MAYA_LOCATION", new List<string>(){ rootDir2 + "/Maya2018" } },
                    {"expectedResult", new List<string>(){ 2.ToString() }}
                },
                new Dictionary<string,List<string>>()
                {
                    {"VENDOR_INSTALLS", new List<string>(){ rootDir1 + "/Maya2017/bin/maya.exe", rootDir2 + "/MayaLT2018/bin/maya.exe" } },
                    {"VENDOR_LOCATIONS", new List<string>(){ rootDir1 } },
                    {"MAYA_LOCATION", new List<string>(){ rootDir2 + "/MayaLT2018" } },
                    {"expectedResult", new List<string>(){ 2.ToString() }}
                },
                new Dictionary<string,List<string>>()
                {
                    {"VENDOR_INSTALLS", new List<string>(){ rootDir1 + "/MayaLT2017/bin/maya.exe", rootDir2 + "/Maya2018/bin/maya.exe" } },
                    {"VENDOR_LOCATIONS", new List<string>(){ rootDir1 } },
                    {"MAYA_LOCATION", new List<string>(){ rootDir2 + "/Maya2018" } },
                    {"expectedResult", new List<string>(){ 2.ToString() }}
                },
                new Dictionary<string,List<string>>()
                {
                    {"VENDOR_INSTALLS", new List<string>(){ rootDir1 + "/MayaLT2017/bin/maya.exe", rootDir2 + "/MayaLT2018/bin/maya.exe" } },
                    {"VENDOR_LOCATIONS", new List<string>(){ rootDir1 } },
                    {"MAYA_LOCATION", new List<string>(){ rootDir2 + "/MayaLT2018" } },
                    {"expectedResult", new List<string>(){ 2.ToString() }}
                },
                new Dictionary<string,List<string>>()
                {
                    {"VENDOR_INSTALLS", new List<string>(){ rootDir1 + "/3ds Max 2017/3dsmax.exe", rootDir2 + "/Maya2018/bin/maya.exe" } },
                    {"VENDOR_LOCATIONS", new List<string>(){ rootDir1 } },
                    {"MAYA_LOCATION", new List<string>(){ rootDir2 + "/Maya2018" } },
                    {"expectedResult", new List<string>(){ 2.ToString() }}
                },
                new Dictionary<string,List<string>>()
                {
                    {"VENDOR_INSTALLS", new List<string>(){ rootDir1 + "/3ds Max 2017/3dsmax.exe", rootDir2 + "/MayaLT2018/bin/maya.exe" } },
                    {"VENDOR_LOCATIONS", new List<string>(){ rootDir1 } },
                    {"MAYA_LOCATION", new List<string>(){ rootDir2 + "/MayaLT2018" } },
                    {"expectedResult", new List<string>(){ 2.ToString() }}
                },
                #endregion
            };

            for (int idx = 0; idx < data.Count; idx++)
            {
                List<string> vendorInstallFolders = data[idx]["VENDOR_INSTALLS"];
                string envVendorLocations = string.Join(";", data[idx]["VENDOR_LOCATIONS"].ToArray());
                string envMayaLocation = data[idx]["MAYA_LOCATION"][0];
                int expectedResult = int.Parse(data[idx]["expectedResult"][0]);

                //SetUp
                //make the hierarchy for the single app path we need
                CreateDummyInstalls(vendorInstallFolders);

                TestLocations(envVendorLocations, envMayaLocation, expectedResult);

                //TearDown
                VendorLocations_TearDown(vendorInstallFolders);
            }
        }

        //TearDown
        public void VendorLocations_TearDown(List<string> vendorInstallFolders)
        {
            //Clean up vendor location(s)
            foreach (var vendorLocation in vendorInstallFolders)
            {
                Directory.Delete(Directory.GetParent(Path.GetDirectoryName(vendorLocation)).FullName, true);
            }
        }
        
        public void TestLocations(string vendorLocation, string mayaLocation, int expectedResult)
        {
            //Mayalocation should remain a List because we want to keep using the dictionary which must be of lists (maybe should make an overload)

            //Set Environment Variables
            SetEnvironmentVariables(vendorLocation, mayaLocation);

            //Nullify these lists so that we guarantee that FindDccInstalls will be called.
            ExportSettings.instance.SetDCCOptionNames(null);
            ExportSettings.instance.SetDCCOptionPaths(null);

            GUIContent[] options = ExportSettings.GetDCCOptions();

            #region //LOGS TO DISPLAY WHAT WE'RE TESTING
            Debug.Log("Directories in VendorLocation:");
            foreach (var directory in Directory.GetDirectories(vendorLocation))
            {
                Debug.Log(directory);
            }

            Debug.Log("MAYA_LOCATION: \n" + mayaLocation);

            Debug.Log("END OF TEST \n \n");
            #endregion

            Assert.AreEqual(options.Length, expectedResult);

        }

        /// <summary>
        /// Sets environment variables to what is passed in, resets the dccOptionNames & dccOptionPaths, and calls FindDCCInstalls()
        /// </summary>
        /// <param name="vendorLocations"></param>
        /// <param name="mayaLocationPath"></param>
        public void SetEnvironmentVariables(string vendorLocation, string mayaLocationPath)
        {
            if (vendorLocation != null)
            {
                //if the given vendor location isn't null, set the environment variable to it.
                System.Environment.SetEnvironmentVariable("UNITY_FBX_3DAPP_VENDOR_LOCATIONS", vendorLocation);
            }
            if (mayaLocationPath != null)
            {
                //if the given MAYA_LOCATION isn't null, set the environment variable to it
                System.Environment.SetEnvironmentVariable("MAYA_LOCATION", mayaLocationPath);
            }
        }

        public void CreateDummyInstalls(List<string> paths)
        {
            foreach (var pathToExe in paths)
            {
                //make the directory
                Directory.CreateDirectory(Path.GetDirectoryName(pathToExe));
                if (Path.GetFileName(pathToExe) != null)
                {

                    //make the file (if we can)
                    FileInfo newExe = new FileInfo(pathToExe);
                    using (FileStream s = newExe.Create()) { }
                }
            }
        }

    }
}
