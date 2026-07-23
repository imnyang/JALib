using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace JALib.Core.Setting;

#pragma warning disable CS0649
class JALibSetting : JASetting {
    public bool logPatches;
    public bool logPrefixWarn;
    public bool LogApiRequests;
    public int loggerLogDetail;
    // Entries that are not in this map follow autoUpdateNewMods.
    public bool autoUpdateNewMods = true;
    public Dictionary<string, bool> autoUpdateMods = [];
    public JASetting Beta;

    protected JALibSetting(JAMod mod, JObject jsonObject = null) : base(mod, jsonObject) {
        autoUpdateMods ??= [];
    }
}
