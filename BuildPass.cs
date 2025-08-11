using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarrowBuildHook
{
    public interface IBuildPass
    {
        public void OnBuild() { }
        public int PassPriority { get; }
        public bool PassWhenInactive { get; }
    }
}
