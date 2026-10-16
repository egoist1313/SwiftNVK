using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(Nvk.Cad.Plugin))]
[assembly: CommandClass(typeof(Nvk.Cad.LispBridge))]

namespace Nvk.Cad
{
    public sealed class Plugin : IExtensionApplication
    {
        public void Initialize()
        {
            NvkSession.RootDir = FindRoot();
            var dm = Application.DocumentManager;
            dm.DocumentBecameCurrent += OnDoc;
            dm.DocumentCreated += OnDoc;
            var doc = dm.MdiActiveDocument;
            if (doc != null)
            {
                try
                {
                    NvkLayers.EnsureAll(doc.Database);
                    NvkPalette.SyncFromDoc(doc, true);
                    NvkEpLive.Attach(doc);
                }
                catch { }
                doc.Editor.WriteMessage(
                    "\n[НВК] Nvk.Cad " + NvkSession.Version
                    + "  НВК-ПАНЕЛЬ / NVKPANEL / НВК-ПОСТРОИТЬ");
            }
        }

        static string FindRoot()
        {
            try
            {
                string loc = typeof(Plugin).Assembly.Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    string dir = Path.GetDirectoryName(loc);
                    // bin\v132 → bin → корень проекта
                    string parent = Path.GetDirectoryName(dir);
                    string root = Path.GetDirectoryName(parent);
                    if (!string.IsNullOrEmpty(root)
                        && (Directory.Exists(Path.Combine(root, "src"))
                            || File.Exists(Path.Combine(root, "nvk-log.txt"))))
                        return root;
                    if (!string.IsNullOrEmpty(parent)
                        && string.Equals(Path.GetFileName(dir), "bin", System.StringComparison.OrdinalIgnoreCase))
                        return parent;
                }
            }
            catch { }
            return @"E:\Новая папка\Lich15\Поперечник НВК";
        }

        public void Terminate()
        {
            try
            {
                NvkEpLive.DetachAll();
                var dm = Application.DocumentManager;
                dm.DocumentBecameCurrent -= OnDoc;
                dm.DocumentCreated -= OnDoc;
            }
            catch { }
        }

        static void OnDoc(object sender, DocumentCollectionEventArgs e)
        {
            try
            {
                NvkEpLive.Attach(e.Document);
                NvkPalette.SyncFromDoc(e.Document, false);
            }
            catch { }
        }
    }
}
