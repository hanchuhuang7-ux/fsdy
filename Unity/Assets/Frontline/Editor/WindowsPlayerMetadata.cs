#if UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Frontline.Editor
{
    // Unity embeds the player icon but leaves its stock launcher version information.
    sealed class WindowsPlayerMetadata : IPostprocessBuildWithReport
    {
        static readonly IntPtr VersionResource = new IntPtr(16);
        static readonly IntPtr VersionId = new IntPtr(1);
        const ushort English = 0x0409, Unicode = 1200;
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            var summary = report.summary;
            if (summary.platform != BuildTarget.StandaloneWindows64 ||
                (summary.options & BuildOptions.BuildScriptsOnly) != 0)
                return;

            string executable = summary.outputPath;
            var existing = FileVersionInfo.GetVersionInfo(executable);
            bool unityVersion = (existing.ProductVersion ?? "").StartsWith(Application.unityVersion, StringComparison.Ordinal);
            string application = PlayerSettings.productName, bundle = PlayerSettings.bundleVersion;
            string product = UseProjectName(existing.ProductName, application);
            string description = UseProjectName(existing.FileDescription, application);
            string company = UseProjectName(existing.CompanyName, PlayerSettings.companyName);
            string fileName = UseProjectName(existing.OriginalFilename, Path.GetFileName(executable));
            string internalName = UseProjectName(existing.InternalName, Path.GetFileNameWithoutExtension(fileName));
            string productVersion = unityVersion || string.IsNullOrWhiteSpace(existing.ProductVersion) ? bundle : existing.ProductVersion;
            string fileVersion = unityVersion || string.IsNullOrWhiteSpace(existing.FileVersion) ? bundle : existing.FileVersion;
            if (product == existing.ProductName && description == existing.FileDescription && company == existing.CompanyName &&
                fileName == existing.OriginalFilename && productVersion == existing.ProductVersion && fileVersion == existing.FileVersion)
                return;

            var fields = new List<byte[]> {
                Field("CompanyName", company), Field("FileDescription", description),
                Field("FileVersion", fileVersion), Field("ProductVersion", productVersion),
                Field("InternalName", internalName), Field("OriginalFilename", fileName), Field("ProductName", product)
            };
            AddField(fields, "LegalCopyright", existing.LegalCopyright);
            AddField(fields, "LegalTrademarks", existing.LegalTrademarks);
            AddField(fields, "Comments", existing.Comments);
            AddField(fields, "PrivateBuild", existing.PrivateBuild);
            AddField(fields, "SpecialBuild", existing.SpecialBuild);
            var entries = fields.ToArray();
            ushort[] languages = ResourceLanguages(executable);
            IntPtr update = BeginUpdateResource(executable, false);
            if (update == IntPtr.Zero) throw new Win32Exception();
            try
            {
                foreach (ushort language in languages)
                {
                    byte[] resource = VersionBlock(language == 0 ? English : language, fileVersion, productVersion, entries);
                    if (!UpdateResource(update, VersionResource, VersionId, language, resource, (uint)resource.Length))
                        throw new Win32Exception();
                }
            }
            catch
            {
                EndUpdateResource(update, true);
                throw;
            }
            if (!EndUpdateResource(update, false)) throw new Win32Exception();

            var embedded = FileVersionInfo.GetVersionInfo(executable);
            if (embedded.ProductName != product || embedded.ProductVersion != productVersion)
                throw new BuildFailedException("Windows player metadata could not be read back.");
            UnityEngine.Debug.Log("FRONTLINE_WINDOWS_METADATA_OK " + embedded.ProductName + " " + embedded.ProductVersion);
        }

        static string UseProjectName(string value, string projectValue)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "Unity" || value == "Unity Technologies" ||
                value == "UnityPlayer" || value == "UnityPlayer.exe")
                return projectValue;
            return value;
        }

        static ushort[] ResourceLanguages(string executable)
        {
            var existing = new List<ushort>();
            IntPtr module = LoadLibraryEx(executable, IntPtr.Zero, 0x00000022);
            if (module == IntPtr.Zero) throw new Win32Exception();
            try
            {
                EnumResourceLanguages(module, VersionResource, VersionId, (image, type, name, language, data) =>
                {
                    existing.Add(language);
                    return true;
                }, IntPtr.Zero);
            }
            finally { FreeLibrary(module); }
            return existing.Count == 0 ? new[] { English } : existing.ToArray();
        }

        static uint VersionPair(int high, int low) => ((uint)Math.Max(0, high) << 16) | ((uint)Math.Max(0, low) & 0xFFFF);

        static Version NumericVersion(string text) =>
            Version.TryParse(text.Split('-', '+', ' ')[0], out var value) ? value : new Version(0, 0);

        static byte[] VersionBlock(ushort language, string fileVersion, string productVersion, params byte[][] fields)
        {
            var file = NumericVersion(fileVersion);
            var product = NumericVersion(productVersion);
            using var stream = new MemoryStream();
            using var binary = new BinaryWriter(stream);
            binary.Write(0xFEEF04BDu);
            binary.Write(0x00010000u);
            binary.Write(VersionPair(file.Major, file.Minor));
            binary.Write(VersionPair(file.Build, file.Revision));
            binary.Write(VersionPair(product.Major, product.Minor));
            binary.Write(VersionPair(product.Build, product.Revision));
            binary.Write(0x3Fu);
            binary.Write(0u);
            binary.Write(0x00040004u);
            binary.Write(1u);
            binary.Write(0u);
            binary.Write(0u);
            binary.Write(0u);

            var translation = new[] { (byte)language, (byte)(language >> 8), (byte)(Unicode & 255), (byte)(Unicode >> 8) };
            var strings = Block("StringFileInfo", true, 0, Array.Empty<byte>(),
                Block(language.ToString("X4") + Unicode.ToString("X4"), true, 0, Array.Empty<byte>(), fields));
            var languageInfo = Block("VarFileInfo", true, 0, Array.Empty<byte>(),
                Block("Translation", false, translation.Length, translation));
            return Block("VS_VERSION_INFO", false, (int)stream.Length, stream.ToArray(), strings, languageInfo);
        }

        static byte[] Field(string key, string value) =>
            Block(key, true, value.Length + 1, Encoding.Unicode.GetBytes(value + "\0"));

        static void AddField(List<byte[]> fields, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) fields.Add(Field(key, value));
        }

        static byte[] Block(string key, bool text, int valueLength, byte[] value, params byte[][] children)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((ushort)0);
            writer.Write((ushort)valueLength);
            writer.Write((ushort)(text ? 1 : 0));
            writer.Write(Encoding.Unicode.GetBytes(key + "\0"));
            Pad(writer);
            writer.Write(value);
            foreach (var child in children)
            {
                Pad(writer);
                writer.Write(child);
            }
            stream.Position = 0;
            writer.Write((ushort)stream.Length);
            return stream.ToArray();
        }

        static void Pad(BinaryWriter writer)
        {
            while ((writer.BaseStream.Position & 3) != 0) writer.Write((byte)0);
        }

        delegate bool EnumResourceLanguage(IntPtr module, IntPtr type, IntPtr name, ushort language, IntPtr data);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
        [DllImport("kernel32.dll", EntryPoint = "EnumResourceLanguagesW")]
        static extern bool EnumResourceLanguages(IntPtr module, IntPtr type, IntPtr name, EnumResourceLanguage callback, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr BeginUpdateResource(string path, bool deleteExisting);
        [DllImport("kernel32.dll", EntryPoint = "UpdateResourceW", SetLastError = true)]
        static extern bool UpdateResource(IntPtr update, IntPtr type, IntPtr name, ushort language, byte[] data, uint size);
        [DllImport("kernel32.dll", EntryPoint = "EndUpdateResourceW", SetLastError = true)]
        static extern bool EndUpdateResource(IntPtr update, bool discard);
    }
}
#endif
