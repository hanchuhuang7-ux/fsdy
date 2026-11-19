using System;
using System.Runtime.CompilerServices;
#if !UNITY_WEBGL
using System.Threading;
#endif

namespace Frontline.Core
{
    public sealed partial class Battle
    {
        public const int TargetWorkerCap=4;
        public const int ParallelTargetScanThreshold=40;
        public const int ParallelTargetScanMinPairs=2400;
        public int TargetScanWorkers {get;}=1;
        public int ParallelTargetScanTicks {get;private set;}
        public int ParallelTargetScanFallbacks {get;private set;}

        readonly struct TargetQuery
        {
            public readonly V3 position;
            readonly float focusX,focusZ;
            readonly bool flank,hunt,focused;

            public TargetQuery(Soldier own,int tick)
            {
                position=own.entity.position;
                flank=own.isFlank;
                hunt=own.isHunt||own.isMarksman;
                var focus=own.focus;
                focused=focus!=null&&focus.endTick>tick;
                focusX=focused?focus.x:0;
                focusZ=focused?focus.z:0;
            }

            public bool Weighted=>flank||hunt||focused;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float DistanceSquared(V3 other)
            {
                float dx=position.x-other.x,dz=position.z-other.z;
                return dx*dx+dz*dz;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float Weigh(float score,V3 other,bool ranged,bool healer,float cost)
            {
                if(focused&&Catalog.Distance(other.x,other.z,focusX,focusZ)<4)score*=.08f;
                if(flank&&ranged)score*=.42f;
                if(hunt)score*=ranged?.12f*(160/cost):healer?.2f:1.5f;
                return score;
            }
        }

        Soldier ResolveTarget(Soldier own,int index,bool prepared)
        {
#if !UNITY_WEBGL
            if(prepared)
            {
                int candidate=targetScan.Results[index];
                if(candidate==TargetScan.NoTarget)return null;
                if(candidate>=0&&Units[candidate].entity.alive)return Units[candidate];
                // Earlier commands may kill the winner or invalidate a previously retained target.
                ParallelTargetScanFallbacks++;
            }
#endif
            return Target(own);
        }

#if !UNITY_WEBGL
        // The main thread owns all battle state. The worker budget is shared across active battles.
        static readonly object scanGate=new object();
        TargetScan targetScan;

        bool PrepareTargetScan()
        {
            if(2*redTargets.Count*blueTargets.Count<ParallelTargetScanMinPairs||!Monitor.TryEnter(scanGate))return false;
            try
            {
                targetScan??=new TargetScan(Units.Count,TargetScanWorkers);
                if(!targetScan.Prepare(Units,Tick))return false;
                targetScan.Run();
                ParallelTargetScanTicks++;
                return true;
            }
            finally {Monitor.Exit(scanGate);}
        }

        sealed class TargetScan:IDisposable
        {
            public const int NoTarget=-1;
            const int Unscanned=-2;
            // Waking workers only pays when the requests imply enough pairwise scoring.
            readonly Candidate[] red,blue;
            readonly Request[] requests;
            readonly Worker[] workers;
            readonly AutoResetEvent finished=new AutoResetEvent(false);
            int redCount,blueCount,requestCount,remaining;
            bool stopping;
            public readonly int[] Results;

            struct Candidate
            {
                public V3 position;
                public float cost;
                public int index;
                public bool ranged,healer;
            }

            struct Request
            {
                public TargetQuery query;
                public int index;
                public bool isRed;
            }

            sealed class Worker
            {
                public readonly AutoResetEvent Ready=new AutoResetEvent(false);
                public readonly Thread Thread;
                public Exception Error;

                public Worker(TargetScan scan,int partition)
                {
                    Thread=new Thread(()=>scan.RunWorker(this,partition)){IsBackground=true,Name="Frontline target scan"};
                }
            }

            public TargetScan(int count,int concurrency)
            {
                red=new Candidate[count];blue=new Candidate[count];requests=new Request[count];Results=new int[count];
                workers=new Worker[concurrency-1];
            }

            public bool Prepare(System.Collections.Generic.List<Soldier> units,int tick)
            {
                redCount=blueCount=requestCount=0;
                int redRequests=0;
                for(int i=0;i<units.Count;i++)
                {
                    var unit=units[i];
                    if(!unit.entity.alive)continue;
                    if(unit.isRed)redCount++;else blueCount++;
                    if((tick+unit.entity.id)%6!=0&&unit.target!=null&&unit.target.entity.alive)continue;
                    requests[requestCount++]=new Request{index=i,isRed=unit.isRed,query=new TargetQuery(unit,tick)};
                    if(unit.isRed)redRequests++;
                }
                if(redCount+blueCount<ParallelTargetScanThreshold||requestCount<workers.Length+1||
                    redRequests*blueCount+(requestCount-redRequests)*redCount<ParallelTargetScanMinPairs)return false;

                int redIndex=0,blueIndex=0;
                for(int i=0;i<units.Count;i++)
                {
                    Results[i]=Unscanned;
                    var unit=units[i];
                    if(!unit.entity.alive)continue;
                    var candidate=new Candidate{index=i,position=unit.entity.position,cost=unit.definition.cost,
                        ranged=unit.isRanged,healer=unit.canHeal};
                    if(unit.isRed)red[redIndex++]=candidate;else blue[blueIndex++]=candidate;
                }
                return true;
            }

            public void Run()
            {
                remaining=workers.Length;
                for(int i=0;i<workers.Length;i++)
                {
                    if(workers[i]==null)
                    {
                        var worker=new Worker(this,i+1);
                        try {worker.Thread.Start();}
                        catch {worker.Ready.Dispose();throw;}
                        workers[i]=worker;
                    }
                }
                foreach(var worker in workers)worker.Ready.Set();
                try {Scan(0);}
                finally {finished.WaitOne();}
                foreach(var worker in workers)
                    if(worker.Error!=null)throw new InvalidOperationException("Parallel target scan failed",worker.Error);
            }

            void RunWorker(Worker worker,int partition)
            {
                for(;;)
                {
                    worker.Ready.WaitOne();
                    if(stopping)return;
                    try {Scan(partition);}
                    catch(Exception error){worker.Error=error;}
                    finally {if(Interlocked.Decrement(ref remaining)==0)finished.Set();}
                }
            }

            void Scan(int partition)
            {
                int start=requestCount*partition/(workers.Length+1);
                int end=requestCount*(partition+1)/(workers.Length+1);
                for(int i=start;i<end;i++)
                {
                    ref readonly var request=ref requests[i];
                    var opponents=request.isRed?blue:red;
                    int count=request.isRed?blueCount:redCount;
                    int best=NoTarget;
                    float bestScore=float.PositiveInfinity;
                    bool weighted=request.query.Weighted;
                    for(int j=0;j<count;j++)
                    {
                        ref readonly var foe=ref opponents[j];
                        float score=request.query.DistanceSquared(foe.position);
                        if(weighted)score=request.query.Weigh(score,foe.position,foe.ranged,foe.healer,foe.cost);
                        // Strict comparison retains the first candidate on equal scores.
                        if(score<bestScore){bestScore=score;best=foe.index;}
                    }
                    Results[request.index]=best;
                }
            }

            public void Dispose()
            {
                stopping=true;
                foreach(var worker in workers)worker?.Ready.Set();
                foreach(var worker in workers)
                {
                    if(worker==null)continue;
                    worker.Thread.Join();
                    worker.Ready.Dispose();
                }
                finished.Dispose();
            }
        }
#endif

#if UNITY_STANDALONE_WIN && !UNITY_WEBGL
        // Only the opt-in desktop performance runner can assemble two legal rosters per side.
        public static Battle CreatePerformanceStress(Catalog catalog,Deployment input,IWorldFactory factory,int targetScanWorkers)
        {
            if(input.map!="meadow"||input.commander.enabled||input.props.Count!=0||
                input.red.Count!=2*Catalog.MaxUnits||input.blue.Count!=2*Catalog.MaxUnits)
                throw new ArgumentException("Invalid desktop stress configuration",nameof(input));

            var half=Json.Clone(input);
            half.red=input.red.GetRange(0,Catalog.MaxUnits);
            half.blue=input.blue.GetRange(0,Catalog.MaxUnits);
            var validated=catalog.Validate(half);
            half.red=input.red.GetRange(Catalog.MaxUnits,Catalog.MaxUnits);
            half.blue=input.blue.GetRange(Catalog.MaxUnits,Catalog.MaxUnits);
            var rest=catalog.Validate(half);
            validated.red.AddRange(rest.red);
            validated.blue.AddRange(rest.blue);
            return new Battle(catalog,validated,factory,targetScanWorkers,false);
        }
#endif
    }
}
