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
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] class OpenFileName {public int size=Marshal.SizeOf(typeof(OpenFileName));public IntPtr owner,instance;public string filter="JSON 文件\0*.json\0所有文件\0*.*\0\0",customFilter;public int maxCustomFilter,filterIndex=1;public StringBuilder file=new StringBuilder(32768);public int maxFile=32768;public StringBuilder fileTitle;public int maxFileTitle;public string initialDir,title;public int flags=0x00080000|0x00000008;public short fileOffset,fileExtension;public string defaultExt="json";public IntPtr customData,hook;public string template;public IntPtr reserved;public int reserved2,flagsEx;}
        [DllImport("comdlg32.dll",CharSet=CharSet.Unicode)] static extern bool GetOpenFileName([In,Out]OpenFileName value);
        [DllImport("comdlg32.dll",CharSet=CharSet.Unicode)] static extern bool GetSaveFileName([In,Out]OpenFileName value);
#endif
        public static void Import(Action<string> onRead)
        {
            pending=onRead;var receiver=Instance;
#if UNITY_WEBGL && !UNITY_EDITOR
            FrontlineUpload(receiver.gameObject.name);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var dialog=new OpenFileName{title="导入阵型或回放",flags=0x00080000|0x00001000|0x00000800|0x00000008};if(GetOpenFileName(dialog)){var f=new FileInfo(dialog.file.ToString());if(f.Length>64*1024*1024)throw new IOException("文件超过 64MB");receiver.OnFileLoaded(File.ReadAllText(f.FullName));}else pending=null;
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
            var dialog=new OpenFileName{title="导出 "+name,file=new StringBuilder(name,32768),flags=0x00080000|0x00000002|0x00000008};if(GetSaveFileName(dialog))File.WriteAllText(dialog.file.ToString(),text,new UTF8Encoding(false));
#else
            File.WriteAllText(Path.Combine(Application.persistentDataPath,name),text);
#endif
        }
    }
}
