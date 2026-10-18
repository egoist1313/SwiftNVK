using System;

namespace Nvk.Core
{
    /// <summary>
    /// Точки вставки CS / EP / VOL и сдвинутые уровни.
    /// На 43-44-46: CS-сетка 2 в ряду, шагX≈24.2, шагY=100 (CsYgap);
    /// EP = CS+(80,0) пять листов вниз шагY профиля;
    /// VOL = EP+(0,−40) пять блоков вниз, COORD справа, SAMP ниже.
    /// Уровни CS/EP после «Двигать» хранятся отдельно и едут вместе с точкой.
    /// </summary>
    public sealed class SiteLayout
    {
        public const double DefaultEpDx = 80.0;
        public const double DefaultEpDy = 0.0;
        public const double DefaultVolFromEpDx = 0.0;
        public const double DefaultVolFromEpDy = -40.0;
        public const double DefaultVolFromCsDx = 0.0;
        public const double DefaultVolFromCsDy = -80.0;

        public double Ox, Oy;
        public bool HasOrigin;

        public double CsX, CsY; public bool HasCs;
        public double EpX, EpY; public bool HasEp;
        public double VolX, VolY; public bool HasVol;
        /// <summary>Сечений в ряду. 2 = как 43-44-46. 0 = по длине продольника.</summary>
        public int Cols;

        public readonly double[] CsLx = new double[5];
        public readonly double[] CsLy = new double[5];
        public readonly bool[] CsL = new bool[5];
        public readonly double[] EpLx = new double[5];
        public readonly double[] EpLy = new double[5];
        public readonly bool[] EpL = new bool[5];

        public bool Any
        {
            get
            {
                if (HasCs || HasEp || HasVol) return true;
                for (int i = 0; i < 5; i++)
                    if (CsL[i] || EpL[i]) return true;
                return false;
            }
        }

        public void SetCs(double x, double y)
        {
            CsX = x; CsY = y; HasCs = true;
        }

        public void SetEp(double x, double y)
        {
            EpX = x; EpY = y; HasEp = true;
        }

        public void SetVol(double x, double y)
        {
            VolX = x; VolY = y; HasVol = true;
        }

        public void SetCsLvl(int i, double x, double y)
        {
            if (i < 0 || i > 4) return;
            CsLx[i] = x; CsLy[i] = y; CsL[i] = true;
        }

        public void SetEpLvl(int i, double x, double y)
        {
            if (i < 0 || i > 4) return;
            EpLx[i] = x; EpLy[i] = y; EpL[i] = true;
        }

        /// <summary>Начало шаблона: точка CS-DRAW, иначе первый уровень, иначе EP/VOL.</summary>
        public void ResolveOrigin()
        {
            if (HasCs) { Ox = CsX; Oy = CsY; HasOrigin = true; return; }
            for (int i = 0; i < 5; i++)
            {
                if (CsL[i]) { Ox = CsLx[i]; Oy = CsLy[i]; HasOrigin = true; return; }
            }
            if (HasEp) { Ox = EpX; Oy = EpY; HasOrigin = true; return; }
            for (int i = 0; i < 5; i++)
            {
                if (EpL[i]) { Ox = EpLx[i]; Oy = EpLy[i]; HasOrigin = true; return; }
            }
            if (HasVol) { Ox = VolX; Oy = VolY; HasOrigin = true; return; }
            HasOrigin = false;
        }

        /// <summary>Перенести раскладку так, чтобы начало совпало с (nx, ny).</summary>
        public SiteLayout At(double nx, double ny)
        {
            ResolveOrigin();
            var r = Clone();
            if (!HasOrigin)
            {
                r.SetCs(nx, ny);
                r.FillDefaults();
                r.Ox = nx; r.Oy = ny; r.HasOrigin = true;
                return r;
            }
            double dx = nx - Ox, dy = ny - Oy;
            r.Shift(dx, dy);
            r.Ox = nx; r.Oy = ny; r.HasOrigin = true;
            if (!r.HasCs) r.SetCs(nx, ny);
            r.FillDefaults();
            return r;
        }

        public void FillDefaults()
        {
            if (!HasCs && HasOrigin) SetCs(Ox, Oy);
            if (!HasEp && HasCs)
                SetEp(CsX + DefaultEpDx, CsY + DefaultEpDy);
            if (!HasVol && HasEp)
                SetVol(EpX + DefaultVolFromEpDx, EpY + DefaultVolFromEpDy);
            else if (!HasVol && HasCs)
                SetVol(CsX + DefaultVolFromCsDx, CsY + DefaultVolFromCsDy);
        }

        public void Shift(double dx, double dy)
        {
            if (HasCs) { CsX += dx; CsY += dy; }
            if (HasEp) { EpX += dx; EpY += dy; }
            if (HasVol) { VolX += dx; VolY += dy; }
            for (int i = 0; i < 5; i++)
            {
                if (CsL[i]) { CsLx[i] += dx; CsLy[i] += dy; }
                if (EpL[i]) { EpLx[i] += dx; EpLy[i] += dy; }
            }
            if (HasOrigin) { Ox += dx; Oy += dy; }
        }

        public SiteLayout Clone()
        {
            var r = new SiteLayout
            {
                Ox = Ox, Oy = Oy, HasOrigin = HasOrigin,
                CsX = CsX, CsY = CsY, HasCs = HasCs,
                EpX = EpX, EpY = EpY, HasEp = HasEp,
                VolX = VolX, VolY = VolY, HasVol = HasVol,
                Cols = Cols
            };
            for (int i = 0; i < 5; i++)
            {
                r.CsLx[i] = CsLx[i]; r.CsLy[i] = CsLy[i]; r.CsL[i] = CsL[i];
                r.EpLx[i] = EpLx[i]; r.EpLy[i] = EpLy[i]; r.EpL[i] = EpL[i];
            }
            return r;
        }
    }
}
