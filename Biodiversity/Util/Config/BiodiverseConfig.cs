﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine.Timeline;

namespace Biodiversity.Util.Config;
[Serializable]
internal abstract class BiodiverseConfig<T> where T : BiodiverseConfig<T> {
    
}