using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Util;
internal static class ExtensionMethods {
    internal static Vector3 Direction(this Vector3 from, Vector3 to) {
        return (to - from).normalized;
    }
}
