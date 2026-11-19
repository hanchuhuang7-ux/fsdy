using System;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class FrontlineApp
    {
        internal const int ManorSaveSlotCount=3;

        static string ManualManorKey(int index)
        {
            if((uint)index>=ManorSaveSlotCount)throw new ArgumentOutOfRangeException(nameof(index));
            return ManorKey+".slot."+(index+1);
        }

        internal bool HasManorSave(int index) => PlayerPrefs.HasKey(ManualManorKey(index));

        internal Manor ReadManorSave(int index)
        {
            string text=PlayerPrefs.GetString(ManualManorKey(index),"");
            if(text.Length==0)throw new InvalidOperationException("此存档槽为空");
            return Manor.Import(text);
        }

        internal void SaveManorSlot(int index,bool overwrite)
        {
            if(Phase!="manor")throw new InvalidOperationException("请先返回庄园");
            string key=ManualManorKey(index);
            if(!overwrite&&PlayerPrefs.HasKey(key))throw new InvalidOperationException("请确认覆盖此存档");
            PlayerPrefs.SetString(key,Estate.Export());
            PlayerPrefs.Save();
            Tell("庄园已保存至槽 "+(index+1));
        }

        internal void LoadManorSlot(int index)
        {
            string text=PlayerPrefs.GetString(ManualManorKey(index),"");
            if(text.Length==0)throw new InvalidOperationException("此存档槽为空");
            ImportManor(text);
            Tell("已载入庄园存档槽 "+(index+1));
        }

        internal static void ResetManorQaSlots()
        {
            for(int i=0;i<ManorSaveSlotCount;i++)PlayerPrefs.DeleteKey(ManualManorKey(i));
        }
    }
}
