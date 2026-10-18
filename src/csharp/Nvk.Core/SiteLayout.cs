using System;

namespace Nvk.Core
{
    /// <summary>
    /// Точки вставки CS / EP / VOL и сдвинутые уровни.
    /// Дефолт как NOD 43-44-46: VOL у точки CS+(0,−40);
    /// уровни L1…L5 в ряд вправо шаг 700; CS каждого уровня
    /// справа от EP того же уровня (CS L1 = CS+(771.8,−190)).
    /// Сетка сечений 2 в ряду, шагX≈24.2, шагY=100.
    /// Уровни после «Двигать» едут вместе с точкой.
    /// </summary>
    public sealed class SiteLayout
    {
        public const double DefaultEpDx = 80.0;
        public const double DefaultEpDy = 0.0;
        public const double DefaultVolDx = 0.0;
        public const double DefaultVolDy = -40.0;
        /// <summary>Шаг X между L1…L5. Лог 43-44-46: EP/CS L2−L1 = 700.</summary>
        public const double DefaultLevelPitchX = 700.0;
        /// <summary>CS L1 относительно CS-DRAW. 2343768.9 − 2342997.1.</summary>
        public const double DefaultCsLvlDx = 771.8;
        public const double DefaultCsLvlDy = -190.0;
        /// <summary>EP L1 относительно CS-DRAW. 2343637.6 − 2342997.1.</summary>
        public const double DefaultEpLvlDx = 640.5;
        public const double DefaultEpLvlDy = 0.0;
        public const int DefaultCols = 2;

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

        /// <summary>Раскладка NOD 43-44-46 в начале координат — база для вставки по точке.</summary>
        public static SiteLayout NodDefault()
        {
            var t = new SiteLayout();
            t.SetCs(0.0, 0.0);
            t.Ox = 0.0;
            t.Oy = 0.0;
            t.HasOrigin = true;
            t.FillDefaults();
            return t;
        }

        public void FillDefaults()
        {
            if (!HasCs && HasOrigin) SetCs(Ox, Oy);
            if (!HasEp && HasCs)
                SetEp(CsX + DefaultEpDx, CsY + DefaultEpDy);
            if (!HasVol && HasCs)
                SetVol(CsX + DefaultVolDx, CsY + DefaultVolDy);
            else if (!HasVol && HasEp)
                SetVol(EpX - DefaultEpDx + DefaultVolDx, EpY + DefaultVolDy);
            if (Cols <= 0) Cols = DefaultCols;
            FillLevelDefaults();
        }

        /// <summary>
        /// Если уровней ещё нет — L1…L5 как в NOD 43-44-46 (ряд вправо).
        /// Уже сдвинутые «Двигать» не трогаем.
        /// </summary>
        public void FillLevelDefaults()
        {
            if (!HasCs) return;
            for (int i = 0; i < 5; i++)
                if (CsL[i] || EpL[i]) return;
            for (int i = 0; i < 5; i++)
            {
                double px = i * DefaultLevelPitchX;
                SetCsLvl(i, CsX + DefaultCsLvlDx + px, CsY + DefaultCsLvlDy);
                SetEpLvl(i, CsX + DefaultEpLvlDx + px, CsY + DefaultEpLvlDy);
            }
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
