using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Util.Logging;
internal class BioDiskLogListener : DiskLogListener {
    public BioDiskLogListener(string localPath, LogLevel displayedLogLevel = LogLevel.Info) : base(localPath, displayedLogLevel, false, false) {
    }
}
