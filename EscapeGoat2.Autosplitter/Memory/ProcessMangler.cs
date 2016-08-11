using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime;
using LiveSplit.EscapeGoat2.Debugging;

namespace LiveSplit.EscapeGoat2.Memory
{
    public class ProcessMangler : IDisposable
    {
        const uint AttachTimeout = 5000;
        const AttachFlag AttachMode = AttachFlag.Passive;

        public readonly DataTarget DataTarget;
        public readonly ClrRuntime Runtime;
        public readonly ClrHeap Heap;
        private Dictionary<string, ClrType> typeCache = new Dictionary<string, ClrType>();

        public ProcessMangler(int processId) {
            DataTarget = DataTarget.AttachToProcess(processId, AttachTimeout, AttachMode);
            Runtime = DataTarget.ClrVersions.First().CreateRuntime();
            Heap = Runtime.GetHeap();

            // latency's bad, let's populate the type cache
            foreach (string type in new string[] {
                "System.Int64",
                "System.Object[]",
                "System.Collections.Generic.List<T>",
                "System.Collections.Generic.List`1",
                "MagicalTimeBean.Bastille.BastilleGame",
                "MagicalTimeBean.Bastille.Scenes.SceneManager",
                "MagicalTimeBean.Bastille.LevelData.MapPosition",
            })
                GetTypeByName(type, false);
        }

        public IEnumerable<ClrRoot> StackLocals {
            get {
                foreach (var thread in Runtime.Threads) {
                    foreach (var r in thread.EnumerateStackObjects())
                        yield return r;
                }
            }
        }

        public IEnumerable<ValuePointer> AllValuesOfType(params ClrType[] types) {
            return AllValuesOfType((IEnumerable<ClrType>)types);
        }

        private IEnumerable<ValuePointer> AllValuesOfType(IEnumerable<ClrType> types) {
            var hs = new HashSet<int>(from t in types select t.Index);

            return from o in Heap.EnumerateObjectAddresses()
                   let t = Heap.GetObjectType(o)
                   where hs.Contains(t.Index)
                   select new ValuePointer(o, t, this);
        }

        public IEnumerable<ValuePointer> AllValuesOfType(params string[] typeNames) {
            return AllValuesOfType(from tn in typeNames select GetTypeByName(tn));
        }

        public ClrType GetTypeByName(string typename, bool log=true)
        {
            if (typeCache.ContainsKey(typename))
                return typeCache[typename];
            ClrType ret = Heap.GetTypeByName(typename);
            if (ret != null)
                typeCache[typename] = ret;
            else if (log)
                LogWriter.WriteLine("Type lookup failed: {0}", typename);
            return ret;
        }

        public ValuePointer? this[ulong address] {
            get {
                var t = Heap.GetObjectType(address);
                if (t == null)
                    return null;

                return new ValuePointer(address, t, this);
            }
        }

        public void Dispose() {
            DataTarget.Dispose();
        }
    }
}
