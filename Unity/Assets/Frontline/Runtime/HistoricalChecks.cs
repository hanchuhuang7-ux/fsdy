using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public static class HistoricalChecks
    {
        [Serializable] sealed class Golden {public string engine,winner,sha256;public int tick;}
        public static void Run(Catalog data,string output)
        {
            foreach(var golden in Json.Read<List<Golden>>(Resources.Load<TextAsset>("history-goldens").text))
            {
                var config=data.Default();config.commander.enabled=true;
                using(IRecordedBattle b=golden.engine=="unity-0.8.0"?(IRecordedBattle)new Frontline.Core.History08.Battle(data,config,new Unity08WorldFactory()):new Frontline.Core.History07.Battle(data,config,golden.engine=="unity-0.6.0"?(IWorldFactory)new LegacyUnityWorldFactory():new Unity07WorldFactory()))
                {
                    var frames=new List<Frame>{b.Snapshot()};b.UseOrderLog(new List<OrderEntry>{new OrderEntry{id=1,team="red",type="inspire",x=-6,z=0,tick=0,endTick=300}});
                    while(b.Result==null){b.Step();if(b.Tick%6==0)frames.Add(b.Snapshot());}
                    if(frames[frames.Count-1].tick!=b.Tick)frames.Add(b.Snapshot());
                    string json=Json.Write(Replay.Create(data,config,frames,b.Result,false,golden.engine));
                    string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-","").ToLowerInvariant();
                    File.WriteAllText(Path.Combine(output,golden.engine+".replay.json"),json,new UTF8Encoding(false));
                    if(b.Tick!=golden.tick||b.Result.winner!=golden.winner||hash!=golden.sha256)throw new InvalidOperationException("Historical full trajectory changed: "+golden.engine+" tick="+b.Tick+" hash="+hash+" expected="+golden.sha256);
                    Debug.Log("FRONTLINE_HISTORY_OK "+golden.engine+" ticks="+b.Tick+" winner="+b.Result.winner+" sha256="+hash);
                }
            }
        }
    }
}
