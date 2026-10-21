namespace Littool
{
    /// <summary>Constants shared by every LITTOOL tab.</summary>
    internal static class LittoolInfo
    {
        public const string Name = "LITTOOL";
        public const string Version = "1.3.4";
        public const string MenuPath = "Tools/LITTOOL";

        /// <summary>GUID of Editor/Images/LITTOOL_Banner.png. Kept stable across versions.</summary>
        public const string BannerGuid = "7e6c8dd50aa852f0a8c6ee2a66fd934c";

        /// <summary>Name of the banner texture, used as a fallback search if the GUID lookup fails.</summary>
        public const string BannerName = "LITTOOL_Banner";

        /// <summary>GUID of Editor/Images/LITTOOL_Silhouette.png (set by the package build).</summary>
        public const string SilhouetteGuid = "cdb175833199590f864327bed1e574a2";

        public const string SilhouetteName = "LITTOOL_Silhouette";

        /// <summary>GUID of Editor/Images/LITTOOL_Globe.png (set by the package build).</summary>
        public const string GlobeGuid = "e36d3d55df79513d8a5a90037c1eafa6";

        public const string GlobeName = "LITTOOL_Globe";

        /// <summary>GUID of Editor/ErrorFinder/ErrorCatalog.json, the Error Finder's error list (set by the package build).</summary>
        public const string ErrorCatalogGuid = "390cf2ad99a058de9262e8334900850a";
    }
}
