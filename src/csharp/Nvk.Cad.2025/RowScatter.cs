using System.Collections.Generic;
using Nvk.Core;

namespace Nvk.Cad
{
    /// <summary>
    /// Рандом со строк таблицы. Набор чисел — у линии (L1 дно, L2 песок…):
    /// дно траншеи на разработке и на песке одно и то же.
    /// </summary>
    static class RowScatter
    {
        static readonly ProfileKind[] AllKinds =
        {
            ProfileKind.Razrabotka, ProfileKind.Pesok, ProfileKind.Truba,
            ProfileKind.Zashita, ProfileKind.Zasypka
        };

        public static List<TableRowDef> Defs(ProfileKind kind)
        {
            var d = NvkSession.GetLevelRows(kind);
            if (d == null || d.Count == 0)
                return TableRowDef.Defaults(kind);
            ApplyLegacy(d, kind);
            return d;
        }

        public static void ApplyLegacy(List<TableRowDef> defs, ProfileKind kind)
        {
            if (defs == null) return;
            foreach (var x in defs)
                if (x.AmpInRecord) return;
            foreach (var x in defs)
            {
                if (x.Type != SheetRowKind.Height || x.UseScatter) continue;
                bool on = false;
                double amp = Scatter.DefaultAmp(kind);
                switch (kind)
                {
                    case ProfileKind.Razrabotka:
                        on = x.Src == HeightSrc.TrenchBottom && NvkUiState.UseScatterRazr;
                        amp = NvkUiState.ScatterRazr;
                        break;
                    case ProfileKind.Pesok:
                        on = (x.Src == HeightSrc.SandTop || x.Src == HeightSrc.TrenchBottom)
                            && NvkUiState.UseScatterRazr;
                        amp = NvkUiState.ScatterRazr;
                        break;
                    case ProfileKind.Truba:
                        on = x.Src == HeightSrc.Invert && NvkUiState.UseScatterTruba;
                        amp = NvkUiState.ScatterTruba;
                        break;
                    case ProfileKind.Zashita:
                        on = x.Src == HeightSrc.ZashTop && NvkUiState.UseScatterZashita;
                        amp = NvkUiState.ScatterZashita;
                        break;
                    case ProfileKind.Zasypka:
                        on = x.Src == HeightSrc.Surface && NvkUiState.UseScatterZasypka;
                        amp = NvkUiState.ScatterZasypka;
                        break;
                }
                if (on)
                {
                    x.UseScatter = true;
                    x.ScatterAmp = amp > 1e-9 ? amp : Scatter.DefaultAmp(kind);
                }
            }
        }

        static bool NoScatter(HeightSrc src)
            => src == HeightSrc.RoadTop || src == HeightSrc.BlackEarth || src == HeightSrc.DeltaZash;

        public static double Amp(HeightSrc src)
        {
            if (NoScatter(src)) return 0.0;
            double a = AmpIn(Scatter.SeriesOf(src), src);
            if (a > 1e-9) return a;
            foreach (var k in AllKinds)
            {
                a = AmpIn(k, src);
                if (a > 1e-9) return a;
            }
            return 0.0;
        }

        public static double Amp(ProfileKind kind, HeightSrc src) => Amp(src);

        static double AmpIn(ProfileKind kind, HeightSrc src)
        {
            var defs = Defs(kind);
            if (defs == null) return 0.0;
            foreach (var d in defs)
            {
                if (d.Type != SheetRowKind.Height || d.Src != src || !d.Show) continue;
                if (!d.UseScatter) continue;
                return d.ScatterAmp > 1e-9 ? d.ScatterAmp : Scatter.DefaultAmp(Scatter.SeriesOf(src));
            }
            return 0.0;
        }

        public static List<double> Vals(DomainResult last, HeightSrc src)
        {
            return last == null ? null : last.Vals(Scatter.SeriesOf(src));
        }

        public static List<double> Vals(DomainResult last, ProfileKind kind)
        {
            return last == null ? null : last.Vals(kind);
        }

        public static bool Ready(DomainResult last, HeightSrc src)
        {
            if (Amp(src) <= 1e-9) return false;
            var v = Vals(last, src);
            return v != null && v.Count > 0;
        }

        public static bool Ready(DomainResult last, ProfileKind kind, HeightSrc src)
            => Ready(last, src);

        public static void Ensure(HeightSrc src, double amp)
        {
            if (NoScatter(src)) return;
            if (amp < 1e-9) amp = Scatter.DefaultAmp(Scatter.SeriesOf(src));
            PatchSrc(src, true, amp);
        }

        /// <summary>Галочка и ±м одной линии (дно / песок / защита…) копируются на все листы, где эта линия есть.</summary>
        public static void SyncFrom(IList<TableRowDef> defs)
        {
            if (defs == null) return;
            foreach (var d in defs)
            {
                if (d.Type != SheetRowKind.Height || NoScatter(d.Src)) continue;
                PatchSrc(d.Src, d.UseScatter, d.ScatterAmp);
            }
        }

        /// <summary>Лист-хозяин линии задаёт Ранд на всех остальных листах.</summary>
        public static void SyncOwners()
        {
            PatchFromOwner(HeightSrc.TrenchBottom);
            PatchFromOwner(HeightSrc.SandTop);
            PatchFromOwner(HeightSrc.Invert);
            PatchFromOwner(HeightSrc.PipeTop);
            PatchFromOwner(HeightSrc.ZashTop);
            PatchFromOwner(HeightSrc.Surface);
        }

        static void PatchFromOwner(HeightSrc src)
        {
            var list = Mutable(Scatter.SeriesOf(src));
            foreach (var x in list)
            {
                if (x.Type != SheetRowKind.Height || x.Src != src) continue;
                PatchSrc(src, x.UseScatter, x.ScatterAmp);
                return;
            }
        }

        static void PatchSrc(HeightSrc src, bool on, double amp)
        {
            foreach (var k in AllKinds)
                PatchKind(k, src, on, amp);
        }

        static void PatchKind(ProfileKind kind, HeightSrc src, bool on, double amp)
        {
            var list = Mutable(kind);
            bool hit = false;
            foreach (var x in list)
            {
                if (x.Type != SheetRowKind.Height || x.Src != src) continue;
                x.UseScatter = on;
                x.ScatterAmp = amp;
                x.AmpInRecord = true;
                hit = true;
            }
            if (hit) Store(kind, list);
        }

        static List<TableRowDef> Mutable(ProfileKind kind)
        {
            var d = NvkSession.GetLevelRows(kind);
            if (d == null || d.Count == 0)
                return TableRowDef.Defaults(kind);
            ApplyLegacy(d, kind);
            return d;
        }

        static void Store(ProfileKind kind, List<TableRowDef> defs)
        {
            string s = TableRowDef.SerializeList(defs);
            switch (kind)
            {
                case ProfileKind.Razrabotka: NvkUiState.RowsL1 = s; break;
                case ProfileKind.Pesok:      NvkUiState.RowsL2 = s; break;
                case ProfileKind.Truba:      NvkUiState.RowsL3 = s; break;
                case ProfileKind.Zashita:    NvkUiState.RowsL4 = s; break;
                case ProfileKind.Zasypka:    NvkUiState.RowsL5 = s; break;
            }
        }
    }
}
