using System.Reflection;
using UnityEngine;

namespace JALib.Tools;

public static class VersionControl {
    public static int releaseNumber;
    public static Version version;

    static VersionControl() {
        FieldInfo field = typeof(GCNS).Field("releaseNumber") ?? typeof(ADOBase).Assembly.GetType(nameof(Releases)).Field(nameof(Releases.releaseNumber));
        releaseNumber = field.GetValue<int>();
#if !TEST
        Version.TryParse(Application.version, out version);
#endif
    }
#if TEST
    internal static void SetupVersion() {
        Version.TryParse(Application.version, out version);
    }
#endif
}