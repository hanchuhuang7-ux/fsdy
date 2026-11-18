using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
namespace Frontline.UnityGame
{
    public sealed class FileBridge:MonoBehaviour
    {
        static FileBridge instance;static Action<string> pending;
        static FileBridge Instance {get{if(!instance){instance=new GameObject("FrontlineFileBridge").AddComponent<FileBridge>();DontDestroyOnLoad(instance.gameObject);}return instance;}}
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void FrontlineDownload(string name,string text);
        [DllImport("__Internal")] static extern void FrontlineUpload(string receiver);
#endif
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // OPENFILENAME layout: mutable UTF-16 buffers are native pointers, not managed class fields.
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct OpenFileName
        {
            public int size;public IntPtr owner,instance;public string filter,customFilter;public int maxCustomFilter,filterIndex;
            public IntPtr file;public int maxFile;public IntPtr fileTitle;public int maxFileTitle;public string initialDir,title;
            public int flags;public short fileOffset,fileExtension;public string defaultExt;public IntPtr customData,hook;public string template;public IntPtr reserved;public int reserved2,flagsEx;
        }
        static OpenFileName Dialog(string title,string name,int flags)
        {
            var value=new OpenFileName{size=Marshal.SizeOf(typeof(OpenFileName)),filter="JSON 文件\0*.json\0所有文件\0*.*\0\0",filterIndex=1,maxFile=32768,title=title,flags=flags,defaultExt="json"};
            // StringBuilder is not valid as a struct field. Own the UTF-16 output buffer explicitly.
            var bytes=new byte[value.maxFile*2];if(!string.IsNullOrEmpty(name))Encoding.Unicode.GetBytes(name,0,name.Length,bytes,0);
            value.file=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,value.file,bytes.Length);return value;
        }
        public static int NativeDialogSize {get{return Marshal.SizeOf(typeof(OpenFileName));}}
        [DllImport("comdlg32.dll",CharSet=CharSet.Unicode)] static extern bool GetOpenFileName(ref OpenFileName value);
        [DllImport("comdlg32.dll",CharSet=CharSet.Unicode)] static extern bool GetSaveFileName(ref OpenFileName value);
#endif
        public static void Import(Action<string> onRead)
        {
            pending=onRead;var receiver=Instance;
#if UNITY_WEBGL && !UNITY_EDITOR
            FrontlineUpload(receiver.gameObject.name);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var dialog=Dialog("导入阵型、回放或庄园",null,0x00080000|0x00001000|0x00000800|0x00000008);try{if(GetOpenFileName(ref dialog)){var f=new FileInfo(Marshal.PtrToStringUni(dialog.file));if(f.Length>64*1024*1024)throw new IOException("文件超过 64MB");receiver.OnFileLoaded(File.ReadAllText(f.FullName));}else pending=null;}finally{Marshal.FreeHGlobal(dialog.file);}
#else
            throw new PlatformNotSupportedException("请使用 JSON 文本导入");
#endif
        }
        public void OnFileLoaded(string text){var callback=pending;pending=null;callback?.Invoke(text);}public void OnFileError(string message){pending=null;Debug.LogWarning(message);}
        public static void Export(string name,string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FrontlineDownload(name,text);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var dialog=Dialog("导出 "+name,name,0x00080000|0x00000002|0x00000008);try{if(GetSaveFileName(ref dialog))File.WriteAllText(Marshal.PtrToStringUni(dialog.file),text,new UTF8Encoding(false));}finally{Marshal.FreeHGlobal(dialog.file);}
#else
            File.WriteAllText(Path.Combine(Application.persistentDataPath,name),text);
#endif
        }
    }
}
