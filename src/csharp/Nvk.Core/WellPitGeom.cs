using System;
using System.Collections.Generic;

namespace Nvk.Core
{
    /// <summary>Настройки котлована колодца. Панель → вкладка «Колодцы».</summary>
    public sealed class WellPitCfg
    {
        /// <summary>Отступ от стенки колодца, м. Полусторона дна = R + отступ.</summary>
        public double TrenchGap = 0.50;
        /// <summary>Откос m = горизонталь/вертикаль. 0 = вертикальные стенки.</summary>
        public double SlopeM = 0.20;
        /// <summary>Толщина подсыпки под подошвой, м. Квадрат дна = отстойник − подсыпка.</summary>
        public double Bedding = 0.10;
        /// <summary>
        /// true — веер 30°/60° из угла квадрата колодца;
        /// false — прямоугольный угол (биссектриса, стороны бровки параллельны дну).
        /// </summary>
        public bool CornerFan = true;
        /// <summary>Масштаб чертежа 1:N для бергштрихов откоса. Шаг 3 мм на бумаге.</summary>
        public int SlopeScaleN = 500;
        /// <summary>Шаг бергштрихов на бумаге, мм. ГОСТ 21.204 / учебные ~2–4 мм.</summary>
        public double SlopeTickPaperMm = 3.0;
    }

    /// <summary>Труба, подходящая к колодцу. Линии — в пределах траншеи до центра / радиуса.</summary>
    public sealed class WellPipeSeg
    {
        public double X0, Y0, X1, Y1;
        public double Invert0, Invert1;
        public double Diam;
        public double FarR;
    }

    /// <summary>Колодец Civil: план, отметки, ось трубы.</summary>
    public sealed class WellPitInput
    {
        public string Name;
        public string Handle;
        public double X;
        public double Y;
        public double Rim;
        public double Sump;
        public double Diam;
        public double Height;
        public double AxisX = 1.0;
        public double AxisY = 0.0;
        public double PipeDiam;
        public int PipeCount;
        public List<WellPipeSeg> Pipes;
    }

    public struct WellPt
    {
        public double X, Y, Z;
        public WellPt(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>Треугольник TIN в метрах. Бровка ловит рёбра этих треугольников.</summary>
    public struct TinTri
    {
        public double X1, Y1, Z1, X2, Y2, Z2, X3, Y3, Z3;
        public TinTri(double x1, double y1, double z1,
            double x2, double y2, double z2,
            double x3, double y3, double z3)
        {
            X1 = x1; Y1 = y1; Z1 = z1;
            X2 = x2; Y2 = y2; Z2 = z2;
            X3 = x3; Y3 = y3; Z3 = z3;
        }
    }

    /// <summary>Готовая геометрия котлована: дно, бровка, разрез.</summary>
    public sealed class WellPitShape
    {
        public List<WellPt> Inner;
        public List<WellPt> Outer;
        public List<List<WellPt>> Bottoms;
        public List<List<WellPt>> Steps;
        public List<List<WellPt>> Berms;
        public List<WellPt> Berm;
        public List<WellPt[]> Faces;
        public List<WellPt[]> SlopeRays;
        /// <summary>Бергштрихи ГОСТ: длинный/короткий от бровки вниз к подошве.</summary>
        public List<WellPt[]> SlopeTicks;
        public List<WellPt> Section;
        public List<WellPt> Shaft;
        public double AlongHalf;
        public double AcrossHalf;
        public double InnerHalf;
        public double BermAlong;
        public double BermAcross;
        public double Ax, Ay, Px, Py;
        public bool HasInner;
    }

    /// <summary>
    /// Котлован колодца в метрах.
    /// Дно — квадрат с отступом на отметке отстойника (это и есть дно котлована).
    /// Откос — профилирование от каждого ребра дна до TIN: бровка по рельефу,
    /// грани откоса 3D. Разрез — трапеция перпендикулярно оси трубы.
    /// </summary>
    public static class WellPitGeom
    {
        public static double Radius(WellPitInput w)
        {
            if (w == null || w.Diam < 0.10) return 0.50;
            return w.Diam * 0.5;
        }

        public static void AxisUnit(WellPitInput w, out double ax, out double ay)
        {
            ax = 1.0;
            ay = 0.0;
            if (w == null) return;
            double len = Math.Sqrt(w.AxisX * w.AxisX + w.AxisY * w.AxisY);
            if (len < 1e-9) return;
            ax = w.AxisX / len;
            ay = w.AxisY / len;
        }

        /// <summary>
        /// Ось трассы из направлений труб. Референс — самый длинный вектор,
        /// остальные разворачиваются, если смотрят в обратную сторону.
        /// </summary>
        public static void CombineDirs(IList<double> dx, IList<double> dy, out double ax, out double ay)
        {
            ax = 1.0;
            ay = 0.0;
            if (dx == null || dy == null || dx.Count == 0 || dx.Count != dy.Count) return;
            int best = 0;
            double bestLen = -1.0;
            for (int i = 0; i < dx.Count; i++)
            {
                double len = Math.Sqrt(dx[i] * dx[i] + dy[i] * dy[i]);
                if (len > bestLen)
                {
                    bestLen = len;
                    best = i;
                }
            }
            if (bestLen < 1e-9) return;
            double rx = dx[best] / bestLen;
            double ry = dy[best] / bestLen;
            double sx = 0.0, sy = 0.0;
            for (int i = 0; i < dx.Count; i++)
            {
                double len = Math.Sqrt(dx[i] * dx[i] + dy[i] * dy[i]);
                if (len < 1e-9) continue;
                double ux = dx[i] / len;
                double uy = dy[i] / len;
                if (ux * rx + uy * ry < 0.0)
                {
                    ux = -ux;
                    uy = -uy;
                }
                sx += ux * len;
                sy += uy * len;
            }
            double L = Math.Sqrt(sx * sx + sy * sy);
            if (L < 1e-9)
            {
                ax = rx;
                ay = ry;
                return;
            }
            ax = sx / L;
            ay = sy / L;
        }

        /// <summary>
        /// Ось = самая длинная труба от колодца. Без биссектрисы на угле.
        /// </summary>
        public static void AxisFromOutgoing(IList<double> ox, IList<double> oy, out double ax, out double ay)
        {
            ax = 1.0;
            ay = 0.0;
            if (ox == null || oy == null || ox.Count == 0 || ox.Count != oy.Count) return;
            int best = 0;
            double bestLen = -1.0;
            for (int i = 0; i < ox.Count; i++)
            {
                double len = Math.Sqrt(ox[i] * ox[i] + oy[i] * oy[i]);
                if (len > bestLen)
                {
                    bestLen = len;
                    best = i;
                }
            }
            if (bestLen < 1e-9) return;
            ax = ox[best] / bestLen;
            ay = oy[best] / bestLen;
        }

        /// <summary>
        /// Ось из квадрата дна: вершины CCW как Rect (0=+A+P … 3=−A+P).
        /// После ROTATE порядок сохраняется — берём ребро вдоль оси.
        /// </summary>
        public static void AxisFromRect(IList<double> x, IList<double> y, out double ax, out double ay)
        {
            ax = 1.0;
            ay = 0.0;
            if (x == null || y == null || x.Count < 4 || y.Count < 4) return;
            double vx = x[0] - x[3];
            double vy = y[0] - y[3];
            double len = Math.Sqrt(vx * vx + vy * vy);
            if (len < 1e-9)
            {
                vx = x[1] - x[2];
                vy = y[1] - y[2];
                len = Math.Sqrt(vx * vx + vy * vy);
            }
            if (len < 1e-9) return;
            ax = vx / len;
            ay = vy / len;
        }

        /// <summary>Касательная оси из нормали сечения Chain (Nx, Ny).</summary>
        public static void TangentFromNormal(double nx, double ny, out double ax, out double ay)
        {
            ax = ny;
            ay = -nx;
            double len = Math.Sqrt(ax * ax + ay * ay);
            if (len < 1e-9)
            {
                ax = 1.0;
                ay = 0.0;
                return;
            }
            ax /= len;
            ay /= len;
        }

        public static void PerpOf(double ax, double ay, out double px, out double py)
        {
            px = -ay;
            py = ax;
        }

        /// <summary>Полусторона квадрата дна: R + отступ от колодца.</summary>
        public static double BottomHalf(WellPitInput w, WellPitCfg c)
        {
            double r = Radius(w);
            double gap = c == null ? 0.0 : Math.Max(0.0, c.TrenchGap);
            return r + gap;
        }

        /// <summary>Полудлина дна вдоль оси = полусторона квадрата.</summary>
        public static double AlongHalf(WellPitInput w, WellPitCfg c)
        {
            return BottomHalf(w, c);
        }

        /// <summary>Полуширина дна поперёк оси = полусторона квадрата.</summary>
        public static double AcrossHalf(WellPitInput w, WellPitCfg c)
        {
            return BottomHalf(w, c);
        }

        public static double BermOffset(double slopeM, double zSurf, double zSump)
        {
            if (slopeM < 0.0) slopeM = 0.0;
            double dz = zSurf - zSump;
            if (dz < 0.0) dz = 0.0;
            return slopeM * dz;
        }

        /// <summary>
        /// Прямоугольник вокруг центра, стороны параллельны оси (ax,ay) и перпендикуляру.
        /// Порядок CCW: +ось+перп, +ось−перп, −ось−перп, −ось+перп.
        /// </summary>
        public static List<WellPt> Rect(double cx, double cy, double z,
            double ax, double ay, double px, double py,
            double along, double across)
        {
            return new List<WellPt>
            {
                new WellPt(cx + ax * along + px * across, cy + ay * along + py * across, z),
                new WellPt(cx + ax * along - px * across, cy + ay * along - py * across, z),
                new WellPt(cx - ax * along - px * across, cy - ay * along - py * across, z),
                new WellPt(cx - ax * along + px * across, cy - ay * along + py * across, z)
            };
        }

        public static WellPitShape Build(WellPitInput w, WellPitCfg c, Func<double, double, double> tinZ)
        {
            return Build(w, c, tinZ, null);
        }

        public static WellPitShape Build(WellPitInput w, WellPitCfg c, Func<double, double, double> tinZ, IList<TinTri> tris)
        {
            if (w == null) w = new WellPitInput();
            if (c == null) c = new WellPitCfg();
            double ax, ay, px, py;
            AxisUnit(w, out ax, out ay);
            PerpOf(ax, ay, out px, out py);
            double zBot = w.Sump - Math.Max(0.0, c.Bedding);
            double along = BottomHalf(w, c);
            double across = along;
            double zMid = tinZ != null ? tinZ(w.X, w.Y) : w.Rim;
            if (double.IsNaN(zMid) || zMid < 1.0) zMid = w.Rim;

            var bot = Rect(w.X, w.Y, zBot, ax, ay, px, py, along, across);
            var berm = new List<WellPt>();
            var faces = new List<WellPt[]>();
            var rays = new List<WellPt[]>();
            GradeSquare(bot, c.SlopeM, zBot, zMid, tinZ, tris, berm, faces, rays, bot, c.CornerFan);
            double maxOff = 0.0;
            for (int i = 0; i < berm.Count; i++)
            {
                double dx = berm[i].X - w.X, dy = berm[i].Y - w.Y;
                double alongP = Math.Abs(dx * ax + dy * ay);
                double acrossP = Math.Abs(dx * px + dy * py);
                if (alongP - along > maxOff) maxOff = alongP - along;
                if (acrossP - across > maxOff) maxOff = acrossP - across;
            }

            var shape = new WellPitShape
            {
                AlongHalf = along,
                AcrossHalf = across,
                InnerHalf = along,
                Ax = ax,
                Ay = ay,
                Px = px,
                Py = py,
                Outer = bot,
                Berm = berm,
                Faces = faces,
                SlopeRays = rays,
                HasInner = false,
                BermAlong = along + maxOff,
                BermAcross = across + maxOff
            };

            var leftBot = new WellPt(w.X + px * across, w.Y + py * across, zBot);
            var rightBot = new WellPt(w.X - px * across, w.Y - py * across, zBot);
            var leftTop = GradeAlong(leftBot.X, leftBot.Y, zBot, px, py, c.SlopeM, tinZ, zMid, 1.0);
            var rightTop = GradeAlong(rightBot.X, rightBot.Y, zBot, -px, -py, c.SlopeM, tinZ, zMid, 1.0);
            shape.Section = new List<WellPt> { leftTop, leftBot, rightBot, rightTop };

            double r = Radius(w);
            double zSump = w.Sump;
            double zTop = w.Rim > zSump + 0.05 ? w.Rim : zSump + 0.05;
            shape.Shaft = new List<WellPt>
            {
                new WellPt(w.X + px * r, w.Y + py * r, zSump),
                new WellPt(w.X - px * r, w.Y - py * r, zSump),
                new WellPt(w.X - px * r, w.Y - py * r, zTop),
                new WellPt(w.X + px * r, w.Y + py * r, zTop)
            };
            FillSlopeTicks(shape, c);
            return shape;
        }

        const int EdgeSamples = 6;
        const double HitEps = 1e-8;
        const double StripEps = 0.02;

        /// <summary>
        /// Биссектриса угла откоса. runScale подбирается так, чтобы сторона
        /// бровки осталась параллельна дну (для квадрата — √2, угол 90°).
        /// </summary>
        public static void CornerBisector(double n1x, double n1y, double n2x, double n2y,
            out double bx, out double by, out double runScale)
        {
            bx = n1x + n2x;
            by = n1y + n2y;
            double L = Math.Sqrt(bx * bx + by * by);
            if (L < 1e-9)
            {
                bx = n1x;
                by = n1y;
                runScale = 1.0;
                return;
            }
            bx /= L;
            by /= L;
            double d = bx * n1x + by * n1y;
            runScale = Math.Abs(d) < 1e-9 ? 1.0 : 1.0 / d;
        }

        /// <summary>
        /// Два луча веера в секторе откоса. n1, n2 — единичные внешние нормали
        /// смежных рёбер. Для квадрата — 30° и 60°.
        /// </summary>
        public static void CornerRayDirs(double n1x, double n1y, double n2x, double n2y,
            out double r1x, out double r1y, out double r2x, out double r2y)
        {
            double a1 = Math.Atan2(n1y, n1x);
            double a2 = Math.Atan2(n2y, n2x);
            double da = a2 - a1;
            while (da > Math.PI) da -= 2.0 * Math.PI;
            while (da < -Math.PI) da += 2.0 * Math.PI;
            double aMid1 = a1 + da / 3.0;
            double aMid2 = a1 + 2.0 * da / 3.0;
            r1x = Math.Cos(aMid1);
            r1y = Math.Sin(aMid1);
            r2x = Math.Cos(aMid2);
            r2y = Math.Sin(aMid2);
        }

        /// <summary>
        /// Профилирование: от ребра дна наружу, пока откос m не встретит TIN.
        /// runScale=1 — луч перпендикуляр ребру; √2 — биссектриса угла квадрата.
        /// </summary>
        public static WellPt GradeAlong(double x, double y, double zBot,
            double nx, double ny, double m, Func<double, double, double> tinZ,
            double zHint, double runScale)
        {
            return GradeAlongPads(x, y, zBot, nx, ny, m, tinZ, zHint, runScale, null);
        }

        /// <summary>
        /// Профилирование ребра: откос идёт до TIN или до плоскости соседней
        /// подошвы — что ближе. Так внутренние откосы сходятся в общую точку,
        /// а не протыкают друг друга.
        /// </summary>
        static WellPt GradeAlongPads(double x, double y, double zBot,
            double nx, double ny, double m, Func<double, double, double> tinZ,
            double zHint, double runScale, IList<GradePad> pads)
        {
            double nlen = Math.Sqrt(nx * nx + ny * ny);
            if (nlen > 1e-9)
            {
                nx /= nlen;
                ny /= nlen;
            }
            if (runScale < 1e-6) runScale = 1.0;
            double zH = zHint;
            if (double.IsNaN(zH) || zH < zBot + 0.01) zH = zBot + 2.0;
            if (m < 1e-9)
            {
                double z0 = Sample(tinZ, x, y, zH);
                if (z0 < zBot) z0 = zBot;
                return new WellPt(x, y, z0);
            }
            double tHi = m * runScale * (zH - zBot + 6.0);
            if (tHi < 1.0) tHi = 1.0;
            if (tHi > 80.0) tHi = 80.0;
            for (int k = 0; k < 5; k++)
            {
                double cap = CapZ(x + tHi * nx, y + tHi * ny, zH, tinZ, pads, x, y);
                double zs = zBot + tHi / (m * runScale);
                if (cap <= zs) break;
                tHi *= 1.5;
                if (tHi > 80.0) { tHi = 80.0; break; }
            }
            double lo = 0.0, hi = tHi;
            for (int i = 0; i < 28; i++)
            {
                double t = 0.5 * (lo + hi);
                double cap = CapZ(x + t * nx, y + t * ny, zH, tinZ, pads, x, y);
                double zs = zBot + t / (m * runScale);
                if (cap > zs) lo = t;
                else hi = t;
            }
            double tHit = 0.5 * (lo + hi);
            double xh = x + tHit * nx;
            double yh = y + tHit * ny;
            double zsHit = zBot + tHit / (m * runScale);
            double zh = CapZ(xh, yh, zsHit, tinZ, pads, x, y);
            if (zh < zBot) zh = zBot;
            return new WellPt(xh, yh, zh);
        }

        struct GradePad
        {
            public IList<WellPt> Poly;
            public double ZBot;
            public double M;
        }

        static double CapZ(double x, double y, double fallback,
            Func<double, double, double> tinZ, IList<GradePad> pads, double x0, double y0)
        {
            double z = Sample(tinZ, x, y, fallback);
            if (pads == null || pads.Count == 0) return z;
            for (int i = 0; i < pads.Count; i++)
            {
                var pad = pads[i];
                if (pad.Poly == null || pad.Poly.Count < 3) continue;
                if (DistOutside(pad.Poly, x0, y0) <= 0.02) continue;
                double pz = PadZ(pad, x, y);
                if (pz < z) z = pz;
            }
            return z;
        }

        static double PadZ(GradePad pad, double x, double y)
        {
            double d = DistOutside(pad.Poly, x, y);
            if (d <= 1e-9) return pad.ZBot;
            if (pad.M < 1e-9) return 1e9;
            return pad.ZBot + d / pad.M;
        }

        /// <summary>
        /// Внешнее расстояние до выпуклого CW-контура (плоскости откоса).
        /// Внутри ≤ 0.
        /// </summary>
        static double DistOutside(IList<WellPt> poly, double x, double y)
        {
            int n = poly.Count;
            double dMax = -1e9;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                double edx = b.X - a.X, edy = b.Y - a.Y;
                double elen = Math.Sqrt(edx * edx + edy * edy);
                if (elen < 1e-12) continue;
                double nx = -edy / elen, ny = edx / elen;
                double d = (x - a.X) * nx + (y - a.Y) * ny;
                if (d > dMax) dMax = d;
            }
            return dMax;
        }

        static double Sample(Func<double, double, double> tinZ, double x, double y, double fallback)
        {
            if (tinZ == null) return fallback;
            double z = tinZ(x, y);
            if (double.IsNaN(z) || z < 1.0) return fallback;
            return z;
        }

        /// <summary>
        /// Бровка по одному ребру дна: углы + пересечения плоскости откоса с рёбрами TIN.
        /// Без треугольников — равномерные пробы (старое поведение).
        /// </summary>
        public static List<WellPt> BermOnEdge(WellPt a, WellPt b, double zBot, double m,
            Func<double, double, double> tinZ, IList<TinTri> tris)
        {
            var hits = HitsOnEdge(a, b, zBot, m, zBot + 2.0, tinZ, tris);
            var pts = new List<WellPt>(hits.Count);
            for (int i = 0; i < hits.Count; i++) pts.Add(hits[i].P);
            return pts;
        }

        struct BermHit
        {
            public double S;
            public WellPt P;
            public WellPt Bot;
        }

        static List<BermHit> HitsOnEdge(WellPt a, WellPt b, double zBot, double m, double zHint,
            Func<double, double, double> tinZ, IList<TinTri> tris)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            double tx = 1.0, ty = 0.0, nx = 0.0, ny = 1.0;
            if (len > 1e-9)
            {
                tx = dx / len;
                ty = dy / len;
                nx = -ty;
                ny = tx;
            }
            double za = a.Z > 1.0 ? a.Z : zBot;
            double zb = b.Z > 1.0 ? b.Z : zBot;
            double zEdge = 0.5 * (za + zb);
            var hits = new List<BermHit>();
            AddHit(hits, 0.0, a, za, nx, ny, m, tinZ, zHint);
            AddHit(hits, len, b, zb, nx, ny, m, tinZ, zHint);
            if (tris != null && tris.Count > 0)
            {
                for (int i = 0; i < tris.Count; i++)
                {
                    var t = tris[i];
                    IntersectTinEdge(a.X, a.Y, zEdge, tx, ty, nx, ny, len, m,
                        t.X1, t.Y1, t.Z1, t.X2, t.Y2, t.Z2, hits);
                    IntersectTinEdge(a.X, a.Y, zEdge, tx, ty, nx, ny, len, m,
                        t.X2, t.Y2, t.Z2, t.X3, t.Y3, t.Z3, hits);
                    IntersectTinEdge(a.X, a.Y, zEdge, tx, ty, nx, ny, len, m,
                        t.X3, t.Y3, t.Z3, t.X1, t.Y1, t.Z1, hits);
                }
            }
            else if (len > 1e-9)
            {
                for (int k = 1; k < EdgeSamples; k++)
                {
                    double s = len * k / (double)EdgeSamples;
                    double zs = za + (zb - za) * (s / len);
                    var bot = new WellPt(a.X + tx * s, a.Y + ty * s, zs);
                    AddHit(hits, s, bot, zs, nx, ny, m, tinZ, zHint);
                }
            }
            hits.Sort((p, q) => p.S.CompareTo(q.S));
            return DedupeHits(hits);
        }

        static void AddHit(List<BermHit> hits, double s, WellPt bot, double zBot,
            double nx, double ny, double m, Func<double, double, double> tinZ, double zHint)
        {
            var g = GradeAlong(bot.X, bot.Y, zBot, nx, ny, m, tinZ, zHint, 1.0);
            hits.Add(new BermHit { S = s, P = g, Bot = bot });
        }

        static void IntersectTinEdge(
            double ax, double ay, double zBot,
            double tx, double ty, double nx, double ny, double len, double m,
            double px, double py, double pz, double qx, double qy, double qz,
            List<BermHit> hits)
        {
            if (m < 1e-9)
            {
                double s, u;
                if (!SegHit(ax, ay, tx, ty, len, px, py, qx, qy, out s, out u)) return;
                double zWall = pz + u * (qz - pz);
                if (zWall < zBot) zWall = zBot;
                var bot = new WellPt(ax + tx * s, ay + ty * s, zBot);
                hits.Add(new BermHit { S = s, P = new WellPt(bot.X, bot.Y, zWall), Bot = bot });
                return;
            }
            double dx = qx - px, dy = qy - py, dz = qz - pz;
            double t0 = (px - ax) * nx + (py - ay) * ny;
            double dt = dx * nx + dy * ny;
            double denom = dz - dt / m;
            double num = zBot + t0 / m - pz;
            if (Math.Abs(denom) < 1e-12)
            {
                if (Math.Abs(num) > 1e-6) return;
                TryOnPlane(ax, ay, zBot, tx, ty, nx, ny, len, m, px, py, pz, hits);
                TryOnPlane(ax, ay, zBot, tx, ty, nx, ny, len, m, qx, qy, qz, hits);
                return;
            }
            double uHit = num / denom;
            if (uHit < -HitEps || uHit > 1.0 + HitEps) return;
            if (uHit < 0.0) uHit = 0.0;
            if (uHit > 1.0) uHit = 1.0;
            double x = px + uHit * dx;
            double y = py + uHit * dy;
            double z = pz + uHit * dz;
            double t = t0 + uHit * dt;
            double sHit = (x - ax) * tx + (y - ay) * ty;
            if (t < -StripEps || sHit < -StripEps || sHit > len + StripEps) return;
            if (sHit < 0.0) sHit = 0.0;
            if (sHit > len) sHit = len;
            if (z < zBot) z = zBot;
            var botPt = new WellPt(ax + tx * sHit, ay + ty * sHit, zBot);
            hits.Add(new BermHit { S = sHit, P = new WellPt(x, y, z), Bot = botPt });
        }

        static void TryOnPlane(double ax, double ay, double zBot,
            double tx, double ty, double nx, double ny, double len, double m,
            double x, double y, double z, List<BermHit> hits)
        {
            double t = (x - ax) * nx + (y - ay) * ny;
            double s = (x - ax) * tx + (y - ay) * ty;
            if (t < -StripEps || s < -StripEps || s > len + StripEps) return;
            if (s < 0.0) s = 0.0;
            if (s > len) s = len;
            if (z < zBot) z = zBot;
            var bot = new WellPt(ax + tx * s, ay + ty * s, zBot);
            hits.Add(new BermHit { S = s, P = new WellPt(x, y, z), Bot = bot });
        }

        static bool SegHit(double ax, double ay, double tx, double ty, double len,
            double px, double py, double qx, double qy, out double s, out double u)
        {
            s = 0.0;
            u = 0.0;
            double dx = qx - px, dy = qy - py;
            double den = tx * dy - ty * dx;
            if (Math.Abs(den) < 1e-12) return false;
            double rx = px - ax, ry = py - ay;
            s = (rx * dy - ry * dx) / den;
            u = (rx * ty - ry * tx) / den;
            if (s < -StripEps || s > len + StripEps) return false;
            if (u < -HitEps || u > 1.0 + HitEps) return false;
            if (s < 0.0) s = 0.0;
            if (s > len) s = len;
            if (u < 0.0) u = 0.0;
            if (u > 1.0) u = 1.0;
            return true;
        }

        static List<BermHit> DedupeHits(List<BermHit> hits)
        {
            var outHits = new List<BermHit>(hits.Count);
            for (int i = 0; i < hits.Count; i++)
            {
                var h = hits[i];
                if (outHits.Count == 0)
                {
                    outHits.Add(h);
                    continue;
                }
                var p = outHits[outHits.Count - 1];
                double ds = h.S - p.S;
                double ddx = h.P.X - p.P.X, ddy = h.P.Y - p.P.Y, ddz = h.P.Z - p.P.Z;
                if (ds < 0.01 && ddx * ddx + ddy * ddy + ddz * ddz < 1e-4) continue;
                outHits.Add(h);
            }
            return outHits;
        }

        static void GradeSquare(List<WellPt> bot, double m, double zBot, double zHint,
            Func<double, double, double> tinZ, IList<TinTri> tris, List<WellPt> berm, List<WellPt[]> faces,
            List<WellPt[]> rays, IList<WellPt> pitCorners, bool fan)
        {
            GradeSquare(bot, m, zBot, zHint, tinZ, tris, berm, faces, rays, pitCorners, fan, null, null);
        }

        static void GradeSquare(List<WellPt> bot, double m, double zBot, double zHint,
            Func<double, double, double> tinZ, IList<TinTri> tris, List<WellPt> berm, List<WellPt[]> faces,
            List<WellPt[]> rays, IList<WellPt> pitCorners, bool fan,
            IList<GradePad> pads, IList<WellPt> clusterHull)
        {
            int n = bot.Count;
            if (n < 3) return;
            var hull = clusterHull != null && clusterHull.Count >= 3 ? clusterHull : OuterHull(bot, 0.05);
            WellPt lastG = default(WellPt);
            WellPt firstP = default(WellPt);
            bool hasLast = false;
            bool hasFirst = false;
            double firstNx = 0.0, firstNy = 1.0;
            double lastNx = 0.0, lastNy = 1.0;
            for (int e = 0; e < n; e++)
            {
                var a = bot[e];
                var b = bot[(e + 1) % n];
                double edx = b.X - a.X, edy = b.Y - a.Y;
                double elen = Math.Sqrt(edx * edx + edy * edy);
                if (elen < 1e-9) continue;
                double nx = -edy / elen;
                double ny = edx / elen;
                if (e == 0)
                {
                    firstNx = nx;
                    firstNy = ny;
                }
                lastNx = nx;
                lastNy = ny;
                double zEdge = a.Z > 1.0 ? a.Z : zBot;
                var hits = HitsOnEdge(a, b, zEdge, m, zHint, tinZ, tris);
                ClipHitsToPads(hits, nx, ny, m, tinZ, zHint, pads);
                if (hits.Count == 0) continue;
                for (int k = 0; k < hits.Count; k++)
                {
                    var h = hits[k];
                    if (k == 0 && hasLast)
                    {
                        var p = bot[(e - 1 + n) % n];
                        double pdx = a.X - p.X, pdy = a.Y - p.Y;
                        double plen = Math.Sqrt(pdx * pdx + pdy * pdy);
                        double n1x = 0.0, n1y = 1.0;
                        if (plen > 1e-9)
                        {
                            n1x = -pdy / plen;
                            n1y = pdx / plen;
                        }
                        if (ConvexCw(p, a, b) && ShouldEmitCorner(pitCorners, hull, a))
                            EmitCornerMode(fan, a, n1x, n1y, nx, ny, zEdge, m, zHint, tinZ, berm, faces, rays, ref lastG, pads);
                        faces.Add(new[] { a, a, h.P, lastG });
                    }
                    if (IsDaylight(h.P, tinZ, pads, a.X, a.Y))
                        berm.Add(h.P);
                    if (rays != null)
                        rays.Add(new[] { h.Bot, h.P });
                    if (k > 0)
                    {
                        var prev = hits[k - 1];
                        faces.Add(new[] { prev.Bot, h.Bot, h.P, prev.P });
                    }
                    if (!hasFirst)
                    {
                        firstP = h.P;
                        hasFirst = true;
                    }
                    lastG = h.P;
                    hasLast = true;
                }
            }
            if (hasLast && hasFirst)
            {
                var p = bot[n - 1];
                var a0 = bot[0];
                var b0 = bot[1];
                double z0 = a0.Z > 1.0 ? a0.Z : zBot;
                if (ConvexCw(p, a0, b0) && ShouldEmitCorner(pitCorners, hull, a0))
                    EmitCornerMode(fan, a0, lastNx, lastNy, firstNx, firstNy, z0, m, zHint, tinZ, berm, faces, rays, ref lastG, pads);
                faces.Add(new[] { a0, a0, firstP, lastG });
            }
        }

        /// <summary>
        /// Веер только у исходного угла квадрата, который остался внешней
        /// вершиной оболочки общего котлована. Внутренние углы квадратов
        /// на длинной стороне общей траншеи и плечи коридора пропускаем.
        /// </summary>
        static bool ShouldEmitCorner(IList<WellPt> pitCorners, IList<WellPt> hull, WellPt a)
        {
            if (!IsPitCorner(pitCorners, a)) return false;
            return NearPts(hull, a, 0.05);
        }

        static bool IsPitCorner(IList<WellPt> pitCorners, WellPt a)
        {
            if (pitCorners == null || pitCorners.Count == 0) return true;
            return NearPts(pitCorners, a, 0.05);
        }

        static bool NearPts(IList<WellPt> pts, WellPt a, double eps)
        {
            if (pts == null || pts.Count == 0) return false;
            double eps2 = eps * eps;
            for (int i = 0; i < pts.Count; i++)
            {
                double dx = pts[i].X - a.X;
                double dy = pts[i].Y - a.Y;
                if (dx * dx + dy * dy <= eps2) return true;
            }
            return false;
        }

        /// <summary>
        /// Вершины выпуклой оболочки; почти коллинеарные (сдвиг &lt; col м)
        /// с длинной стороны не считаются углами.
        /// </summary>
        static List<WellPt> OuterHull(IList<WellPt> pts, double col)
        {
            var src = new List<WellPt>();
            if (pts == null) return src;
            for (int i = 0; i < pts.Count; i++)
            {
                bool dup = false;
                for (int j = 0; j < src.Count; j++)
                {
                    double dx = src[j].X - pts[i].X, dy = src[j].Y - pts[i].Y;
                    if (dx * dx + dy * dy < 1e-12) { dup = true; break; }
                }
                if (!dup) src.Add(pts[i]);
            }
            if (src.Count <= 3) return src;
            src.Sort((a, b) =>
            {
                int c = a.X.CompareTo(b.X);
                return c != 0 ? c : a.Y.CompareTo(b.Y);
            });
            if (col < 1e-6) col = 1e-6;
            var lower = new List<WellPt>();
            for (int i = 0; i < src.Count; i++)
            {
                while (lower.Count >= 2 && HullCollinear(lower[lower.Count - 2], lower[lower.Count - 1], src[i], col))
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(src[i]);
            }
            var upper = new List<WellPt>();
            for (int i = src.Count - 1; i >= 0; i--)
            {
                while (upper.Count >= 2 && HullCollinear(upper[upper.Count - 2], upper[upper.Count - 1], src[i], col))
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(src[i]);
            }
            if (lower.Count > 0) lower.RemoveAt(lower.Count - 1);
            if (upper.Count > 0) upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        static bool HullCollinear(WellPt o, WellPt a, WellPt b, double col)
        {
            double cr = Cross(o, a, b);
            double dx = b.X - o.X, dy = b.Y - o.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return true;
            return cr <= col * len;
        }

        static bool ConvexCw(WellPt p, WellPt a, WellPt b)
        {
            double v1x = a.X - p.X, v1y = a.Y - p.Y;
            double v2x = b.X - a.X, v2y = b.Y - a.Y;
            return v1x * v2y - v1y * v2x < -1e-12;
        }

        static void ClipHitsToPads(List<BermHit> hits, double nx, double ny, double m,
            Func<double, double, double> tinZ, double zHint, IList<GradePad> pads)
        {
            if (hits == null || pads == null || pads.Count == 0) return;
            for (int i = 0; i < hits.Count; i++)
            {
                var h = hits[i];
                double cap = CapZ(h.P.X, h.P.Y, h.P.Z, tinZ, pads, h.Bot.X, h.Bot.Y);
                if (cap >= h.P.Z - 1e-6) continue;
                h.P = GradeAlongPads(h.Bot.X, h.Bot.Y, h.Bot.Z > 1.0 ? h.Bot.Z : h.P.Z,
                    nx, ny, m, tinZ, zHint, 1.0, pads);
                hits[i] = h;
            }
        }

        static bool IsDaylight(WellPt p, Func<double, double, double> tinZ, IList<GradePad> pads,
            double x0, double y0)
        {
            if (pads == null || pads.Count == 0) return true;
            double tin = Sample(tinZ, p.X, p.Y, p.Z);
            if (tin - p.Z > 0.08) return false;
            double pad = CapZ(p.X, p.Y, 1e9, null, pads, x0, y0);
            return pad > p.Z + 0.08;
        }

        static void EmitCornerMode(bool fan, WellPt corner,
            double n1x, double n1y, double n2x, double n2y,
            double zBot, double m, double zHint,
            Func<double, double, double> tinZ,
            List<WellPt> berm, List<WellPt[]> faces, List<WellPt[]> rays, ref WellPt lastG,
            IList<GradePad> pads)
        {
            if (fan)
                EmitCornerRays(corner, n1x, n1y, n2x, n2y, zBot, m, zHint, tinZ, berm, faces, rays, ref lastG, pads);
            else
                EmitCorner(corner, n1x, n1y, n2x, n2y, zBot, m, zHint, tinZ, berm, faces, rays, ref lastG, pads);
        }

        static void EmitCorner(WellPt corner,
            double n1x, double n1y, double n2x, double n2y,
            double zBot, double m, double zHint,
            Func<double, double, double> tinZ,
            List<WellPt> berm, List<WellPt[]> faces, List<WellPt[]> rays, ref WellPt lastG,
            IList<GradePad> pads)
        {
            if (m < 1e-9) return;
            double bx, by, runScale;
            CornerBisector(n1x, n1y, n2x, n2y, out bx, out by, out runScale);
            var g = GradeAlongPads(corner.X, corner.Y, zBot, bx, by, m, tinZ, zHint, runScale, pads);
            faces.Add(new[] { corner, corner, g, lastG });
            if (IsDaylight(g, tinZ, pads, corner.X, corner.Y)) berm.Add(g);
            if (rays != null) rays.Add(new[] { corner, g });
            lastG = g;
        }

        static void EmitCornerRays(WellPt corner,
            double n1x, double n1y, double n2x, double n2y,
            double zBot, double m, double zHint,
            Func<double, double, double> tinZ,
            List<WellPt> berm, List<WellPt[]> faces, List<WellPt[]> rays, ref WellPt lastG,
            IList<GradePad> pads)
        {
            if (m < 1e-9) return;
            double r1x, r1y, r2x, r2y;
            CornerRayDirs(n1x, n1y, n2x, n2y, out r1x, out r1y, out r2x, out r2y);
            var g1 = GradeAlongPads(corner.X, corner.Y, zBot, r1x, r1y, m, tinZ, zHint, 1.0, pads);
            faces.Add(new[] { corner, corner, g1, lastG });
            if (IsDaylight(g1, tinZ, pads, corner.X, corner.Y)) berm.Add(g1);
            if (rays != null) rays.Add(new[] { corner, g1 });
            lastG = g1;
            var g2 = GradeAlongPads(corner.X, corner.Y, zBot, r2x, r2y, m, tinZ, zHint, 1.0, pads);
            faces.Add(new[] { corner, corner, g2, lastG });
            if (IsDaylight(g2, tinZ, pads, corner.X, corner.Y)) berm.Add(g2);
            if (rays != null) rays.Add(new[] { corner, g2 });
            lastG = g2;
        }

        public static bool PointInPoly(IList<WellPt> poly, double x, double y)
        {
            if (poly == null || poly.Count < 3) return false;
            bool inside = false;
            int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = poly[i].X, yi = poly[i].Y;
                double xj = poly[j].X, yj = poly[j].Y;
                if ((yi > y) == (yj > y)) continue;
                double den = yj - yi;
                if (Math.Abs(den) < 1e-15) continue;
                if (x < (xj - xi) * (y - yi) / den + xi)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// Шаг бергштрихов в модели, м: paperMm/1000 × масштаб 1:N.
        /// 3 мм при 1:500 = 1.5 м.
        /// </summary>
        public static double TickStepM(int scaleN, double paperMm)
        {
            if (scaleN < 1) scaleN = 500;
            if (paperMm < 0.5) paperMm = 3.0;
            return paperMm * 0.001 * scaleN;
        }

        static void FillSlopeTicks(WellPitShape s, WellPitCfg c)
        {
            if (s == null) return;
            if (c != null && c.SlopeM < 1e-9)
            {
                s.SlopeTicks = new List<WellPt[]>();
                return;
            }
            int scale = c != null && c.SlopeScaleN > 0 ? c.SlopeScaleN : 500;
            double paper = c != null ? c.SlopeTickPaperMm : 3.0;
            double step = TickStepM(scale, paper);
            var floors = s.Bottoms;
            if (floors == null || floors.Count == 0)
            {
                if (s.Outer != null && s.Outer.Count >= 3)
                    floors = new List<List<WellPt>> { s.Outer };
            }
            var ticks = new List<WellPt[]>();
            if (s.Berms != null && s.Berms.Count > 0)
            {
                for (int i = 0; i < s.Berms.Count; i++)
                    AppendTicks(ticks, s.Berms[i], floors, step);
            }
            else
                AppendTicks(ticks, s.Berm, floors, step);
            s.SlopeTicks = ticks;
        }

        static void AppendTicks(List<WellPt[]> dst, IList<WellPt> berm,
            IList<List<WellPt>> floors, double stepM)
        {
            var t = MakeSlopeTicks(berm, floors, stepM);
            if (t == null) return;
            for (int i = 0; i < t.Count; i++)
                dst.Add(t[i]);
        }

        /// <summary>
        /// Бергштрихи ГОСТ 21.204 / 21.508: от бровки вниз к подошве,
        /// чередование длинный (3/4 ширины откоса) / короткий (1/4).
        /// Бровка обходится как замкнутый контур; внутрь — внутрь полигона.
        /// </summary>
        public static List<WellPt[]> MakeSlopeTicks(
            IList<WellPt> berm, IList<List<WellPt>> floors, double stepM)
        {
            return MakeSlopeTicks(berm, floors, stepM, 0.75, 0.25);
        }

        public static List<WellPt[]> MakeSlopeTicks(
            IList<WellPt> berm, IList<List<WellPt>> floors, double stepM,
            double longFrac, double shortFrac)
        {
            var ticks = new List<WellPt[]>();
            if (berm == null || berm.Count < 3) return ticks;
            if (floors == null || floors.Count == 0) return ticks;
            if (stepM < 1e-6) stepM = 1.5;
            if (longFrac < 0.05) longFrac = 0.75;
            if (shortFrac < 0.05) shortFrac = 0.25;
            double L = ClosedLen(berm);
            if (L < stepM * 0.4) return ticks;
            int n = (int)Math.Round(L / stepM);
            if (n < 2) n = 2;
            if ((n % 2) != 0) n++;
            double ds = L / n;
            for (int i = 0; i < n; i++)
            {
                WellPt a;
                double inx, iny;
                if (!PointOnClosed(berm, (i + 0.5) * ds, out a, out inx, out iny))
                    continue;
                if (NearAnyFloor(floors, a.X, a.Y, 0.05))
                    continue;
                double test = 0.05;
                if (!PointInPoly(berm, a.X + inx * test, a.Y + iny * test))
                {
                    inx = -inx;
                    iny = -iny;
                    if (!PointInPoly(berm, a.X + inx * test, a.Y + iny * test))
                        continue;
                }
                WellPt toe;
                double dist;
                if (!RayHitFloors(floors, a.X, a.Y, inx, iny, out dist, out toe))
                    continue;
                if (dist < 0.02 || dist > 200.0) continue;
                double frac = (i % 2) == 0 ? longFrac : shortFrac;
                if (frac > 0.98) frac = 0.98;
                ticks.Add(new[]
                {
                    a,
                    new WellPt(
                        a.X + inx * dist * frac,
                        a.Y + iny * dist * frac,
                        a.Z + (toe.Z - a.Z) * frac)
                });
            }
            return ticks;
        }

        static double ClosedLen(IList<WellPt> p)
        {
            double L = 0.0;
            int n = p.Count;
            for (int i = 0; i < n; i++)
            {
                var a = p[i];
                var b = p[(i + 1) % n];
                double dx = b.X - a.X, dy = b.Y - a.Y;
                L += Math.Sqrt(dx * dx + dy * dy);
            }
            return L;
        }

        static bool PointOnClosed(IList<WellPt> p, double s,
            out WellPt pt, out double inx, out double iny)
        {
            pt = p[0];
            inx = 0.0;
            iny = -1.0;
            int n = p.Count;
            double acc = 0.0;
            double per = ClosedLen(p);
            if (per < 1e-9) return false;
            s = s % per;
            if (s < 0.0) s += per;
            for (int i = 0; i < n; i++)
            {
                var a = p[i];
                var b = p[(i + 1) % n];
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double elen = Math.Sqrt(dx * dx + dy * dy);
                if (elen < 1e-9) continue;
                if (acc + elen < s - 1e-12)
                {
                    acc += elen;
                    continue;
                }
                double t = (s - acc) / elen;
                if (t < 0.0) t = 0.0;
                if (t > 1.0) t = 1.0;
                pt = new WellPt(
                    a.X + dx * t,
                    a.Y + dy * t,
                    a.Z + (b.Z - a.Z) * t);
                // внутрь CW-контура = правая нормаль (dy, −dx)
                inx = dy / elen;
                iny = -dx / elen;
                return true;
            }
            return false;
        }

        static bool NearAnyFloor(IList<List<WellPt>> floors, double x, double y, double eps)
        {
            if (floors == null) return false;
            for (int i = 0; i < floors.Count; i++)
            {
                var p = floors[i];
                if (p == null || p.Count < 2) continue;
                int n = p.Count;
                for (int k = 0; k < n; k++)
                {
                    var a = p[k];
                    var b = p[(k + 1) % n];
                    if (DistToSeg(x, y, a, b) <= eps) return true;
                }
            }
            return false;
        }

        static double DistToSeg(double x, double y, WellPt a, WellPt b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            if (len2 < 1e-18)
            {
                dx = x - a.X;
                dy = y - a.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
            double t = ((x - a.X) * dx + (y - a.Y) * dy) / len2;
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
            dx = x - (a.X + t * dx);
            dy = y - (a.Y + t * dy);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        static bool RayHitFloors(IList<List<WellPt>> floors,
            double x, double y, double dx, double dy,
            out double dist, out WellPt hit)
        {
            dist = double.MaxValue;
            hit = default(WellPt);
            bool ok = false;
            if (floors == null) return false;
            for (int i = 0; i < floors.Count; i++)
            {
                double d;
                WellPt h;
                if (!RayHitPoly(floors[i], x, y, dx, dy, out d, out h)) continue;
                if (d < dist)
                {
                    dist = d;
                    hit = h;
                    ok = true;
                }
            }
            return ok;
        }

        static bool RayHitPoly(IList<WellPt> poly,
            double x, double y, double dx, double dy,
            out double dist, out WellPt hit)
        {
            dist = double.MaxValue;
            hit = default(WellPt);
            if (poly == null || poly.Count < 3) return false;
            const double maxT = 500.0;
            bool ok = false;
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                double t, u;
                if (!SegSeg(x, y, x + dx * maxT, y + dy * maxT,
                    a.X, a.Y, b.X, b.Y, out t, out u))
                    continue;
                if (t < 1e-6) continue;
                double d = t * maxT;
                if (d < dist)
                {
                    dist = d;
                    hit = new WellPt(
                        x + dx * d,
                        y + dy * d,
                        a.Z + u * (b.Z - a.Z));
                    ok = true;
                }
            }
            return ok;
        }

        static bool SegSeg(double ax, double ay, double bx, double by,
            double cx, double cy, double dx, double dy, out double t, out double u)
        {
            t = 0.0;
            u = 0.0;
            double rx = bx - ax, ry = by - ay;
            double sx = dx - cx, sy = dy - cy;
            double den = rx * sy - ry * sx;
            if (Math.Abs(den) < 1e-12) return false;
            double qx = cx - ax, qy = cy - ay;
            t = (qx * sy - qy * sx) / den;
            u = (qx * ry - qy * rx) / den;
            return t >= -1e-9 && t <= 1.0 + 1e-9 && u >= -1e-9 && u <= 1.0 + 1e-9;
        }

        /// <summary>
        /// Края траншеи от бровки. throughAxis — сквозная до оси.
        /// toWell — контур прямоугольника траншеи с вырезанным колодцем
        /// (если уже — дуга по стенке; если шире — до оси и полудуга колодца).
        /// </summary>
        public static bool PipeLines(WellPitInput w, WellPipeSeg p, IList<WellPt> berm,
            double hwL, double hwR,
            out List<WellPt> throughAxis, out List<WellPt> toWell)
        {
            throughAxis = toWell = null;
            WellPt bermL, L0, R0, bermR, C;
            double ux, uy, lx, ly, cx, cy, R, z0;
            if (!PipeFrame(w, p, berm, hwL, hwR,
                out ux, out uy, out lx, out ly, out cx, out cy, out R, out z0,
                out bermL, out L0, out R0, out bermR, out C))
                return false;
            if (hwL < 0.05) hwL = 0.05;
            if (hwR < 0.05) hwR = 0.05;
            throughAxis = new List<WellPt> { bermL, L0, C, R0, bermR };
            WellPt joinL = CircleJoin(cx, cy, z0, ux, uy, lx, ly, hwL, R, true);
            WellPt joinR = CircleJoin(cx, cy, z0, ux, uy, lx, ly, hwR, R, false);
            toWell = new List<WellPt> { bermL };
            if (hwL >= R - 1e-9) toWell.Add(L0);
            toWell.Add(joinL);
            AddArc(toWell, cx, cy, z0, R, joinL, joinR, ux, uy);
            toWell.Add(joinR);
            if (hwR >= R - 1e-9) toWell.Add(R0);
            toWell.Add(bermR);
            return true;
        }

        /// <summary>
        /// Прямоугольник траншеи: от оси колодца до бровки (обрезается по бровке).
        /// </summary>
        public static bool TrenchRect(WellPitInput w, WellPipeSeg p, IList<WellPt> berm,
            double hwL, double hwR, out List<WellPt> rect)
        {
            rect = null;
            WellPt bermL, L0, R0, bermR, C;
            double ux, uy, lx, ly, cx, cy, R, z0;
            if (!PipeFrame(w, p, berm, hwL, hwR,
                out ux, out uy, out lx, out ly, out cx, out cy, out R, out z0,
                out bermL, out L0, out R0, out bermR, out C))
                return false;
            rect = new List<WellPt> { bermL, L0, R0, bermR };
            return true;
        }

        /// <summary>Прямоугольник траншеи вдоль всей трубы (без обрезки по бровке).</summary>
        public static bool TrenchCorridor(WellPitInput w, WellPipeSeg p,
            double hwL, double hwR, out List<WellPt> rect)
        {
            rect = null;
            WellPt L0, R0, farL, farR, C, far;
            double ux, uy, lx, ly, cx, cy, R, z0;
            if (!PipeStrip(w, p, hwL, hwR,
                out ux, out uy, out lx, out ly, out cx, out cy, out R, out z0,
                out L0, out R0, out farL, out farR, out C, out far))
                return false;
            rect = new List<WellPt> { L0, farL, farR, R0 };
            return true;
        }

        /// <summary>Красная сквозная: поперечина на оси колодца и ось трубы до дальнего.</summary>
        public static bool ThroughLines(WellPitInput w, WellPipeSeg p,
            double hwL, double hwR, out List<WellPt> cross, out List<WellPt> axis)
        {
            cross = axis = null;
            WellPt L0, R0, farL, farR, C, far;
            double ux, uy, lx, ly, cx, cy, R, z0;
            if (!PipeStrip(w, p, hwL, hwR,
                out ux, out uy, out lx, out ly, out cx, out cy, out R, out z0,
                out L0, out R0, out farL, out farR, out C, out far))
                return false;
            cross = new List<WellPt> { L0, C, R0 };
            axis = new List<WellPt> { C, far };
            return true;
        }

        /// <summary>
        /// Синие контуры одного колодца. Для нескольких колодцев — UnionTrenchesAll.
        /// </summary>
        public static List<PolyLoop> UnionTrenches(
            WellPitInput w, IList<WellPt> berm,
            IList<WellPipeSeg> pipes, IList<double> hwL, IList<double> hwR)
        {
            var rects = new List<List<WellPt>>();
            var dx = new List<double>();
            var dy = new List<double>();
            var dR = new List<double>();
            if (w == null || pipes == null) return new List<PolyLoop>();
            double z = w.Sump;
            dx.Add(w.X);
            dy.Add(w.Y);
            dR.Add(Radius(w));
            for (int i = 0; i < pipes.Count; i++)
            {
                var p = pipes[i];
                if (p == null) continue;
                double l = hwL != null && i < hwL.Count ? hwL[i] : 0.30;
                double r = hwR != null && i < hwR.Count ? hwR[i] : 0.30;
                List<WellPt> rect;
                if (!TrenchRect(w, p, berm, l, r, out rect)) continue;
                rects.Add(rect);
                z = p.Invert0;
                if (p.FarR > 0.05)
                {
                    dx.Add(p.X1);
                    dy.Add(p.Y1);
                    dR.Add(p.FarR);
                }
            }
            return PolyUnion.UnionMinusDisks(rects, dx, dy, dR, z, true);
        }

        public static List<PolyLoop> UnionTrenchesAll(
            IList<List<WellPt>> rects,
            IList<double> wx, IList<double> wy, IList<double> wR, double z)
        {
            return PolyUnion.UnionMinusDisks(rects, wx, wy, wR, z, true);
        }

        /// <summary>Объединение прямоугольников без выреза колодца (красная линия).</summary>
        public static List<PolyLoop> UnionRects(IList<List<WellPt>> rects, double z)
        {
            return PolyUnion.UnionMinusDisks(rects, null, null, null, z, true);
        }

        public static List<PolyLoop> UnionPolygons(IList<List<WellPt>> polys, double z)
        {
            return PolyUnion.UnionMinusDisks(polys, null, null, null, z, false);
        }

        public static bool PolysOverlap(IList<WellPt> a, IList<WellPt> b)
        {
            if (a == null || b == null || a.Count < 3 || b.Count < 3) return false;
            for (int i = 0; i < a.Count; i++)
                if (PointInPoly(b, a[i].X, a[i].Y)) return true;
            for (int i = 0; i < b.Count; i++)
                if (PointInPoly(a, b[i].X, b[i].Y)) return true;
            int na = a.Count, nb = b.Count;
            for (int i = 0; i < na; i++)
            {
                var p = a[i];
                var q = a[(i + 1) % na];
                for (int j = 0; j < nb; j++)
                {
                    var r = b[j];
                    var s = b[(j + 1) % nb];
                    double t, u;
                    if (SegSeg(p.X, p.Y, q.X, q.Y, r.X, r.Y, s.X, s.Y, out t, out u))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Общий котлован: пересекающиеся подошвы сливаются с перепадом по середине;
        /// непересекающиеся остаются отдельными; пересекающиеся откосы режутся
        /// плоскость-о-плоскость (общая бровка, тальвег между подошвами).
        /// </summary>
        public static WellPitShape UnionPits(IList<WellPitShape> shapes, WellPitCfg c,
            Func<double, double, double> tinZ, IList<TinTri> tris, double zHint)
        {
            return UnionPits(shapes, null, c, tinZ, tris, zHint);
        }

        public static WellPitShape UnionPits(IList<WellPitShape> shapes, IList<List<WellPt>> extraPolys,
            WellPitCfg c, Func<double, double, double> tinZ, IList<TinTri> tris, double zHint)
        {
            if (shapes == null || shapes.Count == 0) return null;
            if (shapes.Count == 1 && (extraPolys == null || extraPolys.Count == 0)) return shapes[0];
            if (c == null) c = new WellPitCfg();
            var sites = new List<PitSite>();
            var outers = new List<List<WellPt>>();
            double ax = 1.0, ay = 0.0, px = 0.0, py = 1.0;
            double along = 0.0, across = 0.0;
            double zMin = 1e9;
            for (int i = 0; i < shapes.Count; i++)
            {
                var s = shapes[i];
                if (s == null || s.Outer == null || s.Outer.Count < 3) continue;
                double cx, cy, cz;
                PolyCenter(s.Outer, out cx, out cy, out cz);
                sites.Add(new PitSite { X = cx, Y = cy, Z = cz, Outer = s.Outer });
                outers.Add(s.Outer);
                if (outers.Count == 1)
                {
                    ax = s.Ax;
                    ay = s.Ay;
                    px = s.Px;
                    py = s.Py;
                    along = s.AlongHalf;
                    across = s.AcrossHalf;
                }
                if (cz < zMin) zMin = cz;
            }
            if (outers.Count == 0) return null;
            if (zMin > 1e8) zMin = 0.0;

            int ns = sites.Count;
            var ov = new bool[ns, ns];
            bool anyOv = false;
            for (int i = 0; i < ns; i++)
            {
                for (int j = i + 1; j < ns; j++)
                {
                    if (!PolysOverlap(sites[i].Outer, sites[j].Outer)) continue;
                    ov[i, j] = ov[j, i] = true;
                    anyOv = true;
                }
            }

            var bottoms = new List<List<WellPt>>();
            var steps = new List<List<WellPt>>();
            var stepFaces = new List<WellPt[]>();
            if (!anyOv)
            {
                for (int i = 0; i < ns; i++)
                    bottoms.Add(new List<WellPt>(sites[i].Outer));
            }
            else
            {
                var parent = new int[ns];
                for (int i = 0; i < ns; i++) parent[i] = i;
                for (int i = 0; i < ns; i++)
                    for (int j = i + 1; j < ns; j++)
                        if (ov[i, j])
                        {
                            int ri = i; while (parent[ri] != ri) ri = parent[ri];
                            int rj = j; while (parent[rj] != rj) rj = parent[rj];
                            if (ri != rj) parent[rj] = ri;
                        }
                var groups = new Dictionary<int, List<int>>();
                for (int i = 0; i < ns; i++)
                {
                    int r = i; while (parent[r] != r) r = parent[r];
                    List<int> g;
                    if (!groups.TryGetValue(r, out g))
                    {
                        g = new List<int>();
                        groups[r] = g;
                    }
                    g.Add(i);
                }
                foreach (var kv in groups)
                {
                    var idx = kv.Value;
                    if (idx.Count == 1)
                    {
                        bottoms.Add(new List<WellPt>(sites[idx[0]].Outer));
                        continue;
                    }
                    var cluster = new List<List<WellPt>>();
                    for (int k = 0; k < idx.Count; k++)
                        cluster.Add(sites[idx[k]].Outer);
                    var loops = UnionPolygons(cluster, zMin);
                    List<WellPt> uni = BiggestLoop(loops);
                    if (uni == null || uni.Count < 3)
                    {
                        for (int k = 0; k < idx.Count; k++)
                            bottoms.Add(new List<WellPt>(sites[idx[k]].Outer));
                        continue;
                    }
                    if (SignedArea(uni) > 0.0) uni.Reverse();
                    var sub = new List<PitSite>(idx.Count);
                    for (int k = 0; k < idx.Count; k++)
                        sub.Add(sites[idx[k]]);
                    var stamped = StampZWithSteps(uni, sub, steps, stepFaces);
                    bottoms.Add(stamped);
                }
            }

            var pitCorners = new List<WellPt>();
            for (int i = 0; i < outers.Count; i++)
            {
                var o = outers[i];
                if (o == null) continue;
                for (int j = 0; j < o.Count; j++)
                    pitCorners.Add(o[j]);
            }
            var berm = new List<WellPt>();
            var berms = new List<List<WellPt>>();
            var faces = new List<WellPt[]>();
            var rays = new List<WellPt[]>();
            double zMid = zHint;
            if (double.IsNaN(zMid) || zMid < 1.0) zMid = zMin + 2.0;

            if (!anyOv && ns >= 2)
            {
                var pads = new List<GradePad>(ns);
                for (int i = 0; i < ns; i++)
                    pads.Add(new GradePad { Poly = sites[i].Outer, ZBot = sites[i].Z, M = c.SlopeM });
                var clusterHull = OuterHull(pitCorners, 0.05);
                var unused = new List<WellPt>();
                for (int i = 0; i < ns; i++)
                {
                    var gp = new List<WellPt>(sites[i].Outer);
                    if (SignedArea(gp) > 0.0) gp.Reverse();
                    GradeSquare(gp, c.SlopeM, sites[i].Z, zMid, tinZ, tris,
                        unused, faces, rays, pitCorners, c.CornerFan, pads, clusterHull);
                }
                var bermSrc = new List<List<WellPt>>();
                if (shapes != null)
                {
                    for (int i = 0; i < shapes.Count; i++)
                        if (shapes[i] != null && shapes[i].Berm != null && shapes[i].Berm.Count >= 3)
                            bermSrc.Add(shapes[i].Berm);
                }
                if (bermSrc.Count == 0)
                {
                    for (int i = 0; i < ns; i++)
                        bermSrc.Add(sites[i].Outer);
                }
                var bermLoops = UnionPolygons(bermSrc, zMid);
                CollectBermLoops(bermLoops, tinZ, zMid, berms, ref berm);
            }
            else
            {
                var gradeSrc = new List<List<WellPt>>();
                for (int i = 0; i < outers.Count; i++)
                    gradeSrc.Add(outers[i]);
                if (extraPolys != null)
                {
                    for (int i = 0; i < extraPolys.Count; i++)
                        if (extraPolys[i] != null && extraPolys[i].Count >= 3)
                            gradeSrc.Add(extraPolys[i]);
                }
                var gradeLoops = UnionPolygons(gradeSrc, zMin);
                int nGrade = 0;
                for (int i = 0; i < gradeLoops.Count; i++)
                {
                    if (gradeLoops[i] == null || gradeLoops[i].Pts == null || gradeLoops[i].Pts.Count < 3)
                        continue;
                    if (Math.Abs(SignedArea(gradeLoops[i].Pts)) < 1e-4) continue;
                    var gp = new List<WellPt>(gradeLoops[i].Pts);
                    if (SignedArea(gp) > 0.0) gp.Reverse();
                    AssignNearestZ(gp, sites);
                    GradeSquare(gp, c.SlopeM, zMin, zMid, tinZ, tris, berm, faces, rays, pitCorners, c.CornerFan);
                    nGrade++;
                }
                if (nGrade == 0 && bottoms.Count > 0)
                {
                    var gp = new List<WellPt>(bottoms[0]);
                    if (SignedArea(gp) > 0.0) gp.Reverse();
                    GradeSquare(gp, c.SlopeM, zMin, zMid, tinZ, tris, berm, faces, rays, pitCorners, c.CornerFan);
                }
            }
            for (int i = 0; i < stepFaces.Count; i++)
                faces.Add(stepFaces[i]);

            List<WellPt> outer = bottoms.Count > 0 ? bottoms[0] : outers[0];
            double maxOff = 0.0;
            for (int i = 0; i < berm.Count; i++)
            {
                double dx = berm[i].X - outer[0].X, dy = berm[i].Y - outer[0].Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d > maxOff) maxOff = d;
            }
            var shape = new WellPitShape
            {
                AlongHalf = along,
                AcrossHalf = across,
                InnerHalf = along,
                Ax = ax,
                Ay = ay,
                Px = px,
                Py = py,
                Outer = outer,
                Bottoms = bottoms,
                Steps = steps,
                Berms = berms.Count > 0 ? berms : null,
                Berm = berm,
                Faces = faces,
                SlopeRays = rays,
                HasInner = false,
                BermAlong = along + maxOff,
                BermAcross = across + maxOff
            };
            FillSlopeTicks(shape, c);
            return shape;
        }

        static void CollectBermLoops(List<PolyLoop> loops, Func<double, double, double> tinZ,
            double zHint, List<List<WellPt>> berms, ref List<WellPt> berm)
        {
            if (loops == null) return;
            List<WellPt> biggest = null;
            double bestA = 0.0;
            for (int i = 0; i < loops.Count; i++)
            {
                if (loops[i] == null || loops[i].Pts == null || loops[i].Pts.Count < 3) continue;
                var gp = new List<WellPt>(loops[i].Pts);
                if (Math.Abs(SignedArea(gp)) < 1e-4) continue;
                if (SignedArea(gp) > 0.0) gp.Reverse();
                for (int k = 0; k < gp.Count; k++)
                {
                    var p = gp[k];
                    p.Z = Sample(tinZ, p.X, p.Y, zHint);
                    gp[k] = p;
                }
                berms.Add(gp);
                double a = Math.Abs(SignedArea(gp));
                if (biggest == null || a > bestA)
                {
                    biggest = gp;
                    bestA = a;
                }
            }
            if (biggest != null) berm = biggest;
        }

        struct PitSite
        {
            public double X, Y, Z;
            public List<WellPt> Outer;
        }

        static void PolyCenter(IList<WellPt> p, out double x, out double y, out double z)
        {
            x = y = z = 0.0;
            if (p == null || p.Count == 0) return;
            for (int i = 0; i < p.Count; i++)
            {
                x += p[i].X;
                y += p[i].Y;
                z += p[i].Z;
            }
            double n = p.Count;
            x /= n;
            y /= n;
            z /= n;
        }

        static List<WellPt> BiggestLoop(List<PolyLoop> loops)
        {
            List<WellPt> best = null;
            double bestA = -1.0;
            if (loops == null) return null;
            for (int i = 0; i < loops.Count; i++)
            {
                if (loops[i] == null || loops[i].Pts == null || loops[i].Pts.Count < 3) continue;
                double ar = Math.Abs(SignedArea(loops[i].Pts));
                if (ar > bestA)
                {
                    bestA = ar;
                    best = new List<WellPt>(loops[i].Pts);
                }
            }
            return best;
        }

        static int NearestSite(List<PitSite> sites, double x, double y)
        {
            int best = 0;
            double bestD = 1e99;
            for (int i = 0; i < sites.Count; i++)
            {
                double dx = x - sites[i].X, dy = y - sites[i].Y;
                double d = dx * dx + dy * dy;
                if (d < bestD - 1e-12)
                {
                    bestD = d;
                    best = i;
                }
                else if (Math.Abs(d - bestD) < 1e-12 && sites[i].Z < sites[best].Z)
                    best = i;
            }
            return best;
        }

        static void AssignNearestZ(List<WellPt> loop, List<PitSite> sites)
        {
            if (loop == null || sites == null || sites.Count == 0) return;
            for (int i = 0; i < loop.Count; i++)
            {
                int s = NearestSite(sites, loop[i].X, loop[i].Y);
                loop[i] = new WellPt(loop[i].X, loop[i].Y, sites[s].Z);
            }
        }

        static List<WellPt> StampZWithSteps(List<WellPt> loop, List<PitSite> sites,
            List<List<WellPt>> steps, List<WellPt[]> stepFaces)
        {
            if (loop == null || loop.Count < 3 || sites == null || sites.Count == 0)
                return loop;
            var o = new List<WellPt>();
            int n = loop.Count;
            for (int i = 0; i < n; i++)
            {
                var p = loop[i];
                var q = loop[(i + 1) % n];
                int si = NearestSite(sites, p.X, p.Y);
                int sj = NearestSite(sites, q.X, q.Y);
                o.Add(new WellPt(p.X, p.Y, sites[si].Z));
                if (si == sj) continue;
                double z0 = sites[si].Z, z1 = sites[sj].Z;
                double mx = 0.5 * (sites[si].X + sites[sj].X);
                double my = 0.5 * (sites[si].Y + sites[sj].Y);
                double ux = sites[sj].X - sites[si].X, uy = sites[sj].Y - sites[si].Y;
                double ul = Math.Sqrt(ux * ux + uy * uy);
                if (ul < 1e-9) continue;
                ux /= ul;
                uy /= ul;
                double dp = (p.X - mx) * ux + (p.Y - my) * uy;
                double dq = (q.X - mx) * ux + (q.Y - my) * uy;
                if (dp * dq > -1e-12) continue;
                double t = dp / (dp - dq);
                if (t < 0.0) t = 0.0;
                if (t > 1.0) t = 1.0;
                double hx = p.X + t * (q.X - p.X);
                double hy = p.Y + t * (q.Y - p.Y);
                o.Add(new WellPt(hx, hy, z0));
                if (Math.Abs(z0 - z1) > 1e-4)
                    o.Add(new WellPt(hx, hy, z1));
            }
            for (int i = 0; i < sites.Count; i++)
            {
                for (int j = i + 1; j < sites.Count; j++)
                    AddStepWall(loop, sites[i], sites[j], steps, stepFaces);
            }
            return o;
        }

        static void AddStepWall(IList<WellPt> poly, PitSite a, PitSite b,
            List<List<WellPt>> steps, List<WellPt[]> stepFaces)
        {
            if (Math.Abs(a.Z - b.Z) < 1e-4) return;
            double mx = 0.5 * (a.X + b.X), my = 0.5 * (a.Y + b.Y);
            double ux = b.X - a.X, uy = b.Y - a.Y;
            double ul = Math.Sqrt(ux * ux + uy * uy);
            if (ul < 1e-9) return;
            ux /= ul;
            uy /= ul;
            double px = -uy, py = ux;
            double reach = 80.0;
            var hits = new List<double>();
            var hx = new List<double>();
            var hy = new List<double>();
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var p = poly[i];
                var q = poly[(i + 1) % n];
                double t, u;
                if (!SegSeg(mx - px * reach, my - py * reach, mx + px * reach, my + py * reach,
                    p.X, p.Y, q.X, q.Y, out t, out u))
                    continue;
                if (u < -1e-9 || u > 1.0 + 1e-9) continue;
                double x = p.X + u * (q.X - p.X);
                double y = p.Y + u * (q.Y - p.Y);
                hits.Add((x - mx) * px + (y - my) * py);
                hx.Add(x);
                hy.Add(y);
            }
            if (hits.Count < 2) return;
            int lo = 0, hi = 0;
            for (int i = 1; i < hits.Count; i++)
            {
                if (hits[i] < hits[lo]) lo = i;
                if (hits[i] > hits[hi]) hi = i;
            }
            if (lo == hi) return;
            double zHi = a.Z > b.Z ? a.Z : b.Z;
            double zLo = a.Z < b.Z ? a.Z : b.Z;
            var p0 = new WellPt(hx[lo], hy[lo], zHi);
            var p1 = new WellPt(hx[hi], hy[hi], zHi);
            var p2 = new WellPt(hx[hi], hy[hi], zLo);
            var p3 = new WellPt(hx[lo], hy[lo], zLo);
            steps.Add(new List<WellPt> { p0, p1 });
            stepFaces.Add(new[] { p0, p1, p2, p3 });
        }

        static double SignedArea(IList<WellPt> p)
        {
            double a = 0.0;
            int n = p.Count;
            for (int i = 0; i < n; i++)
            {
                var q = p[(i + 1) % n];
                a += p[i].X * q.Y - q.X * p[i].Y;
            }
            return 0.5 * a;
        }

        static double Cross(WellPt o, WellPt a, WellPt b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }

        /// <summary>Выпуклая оболочка в плане (CCW). Нужна, когда подошвы рядом, но не пересекаются.</summary>
        public static List<WellPt> ConvexHull(IList<WellPt> pts)
        {
            var src = new List<WellPt>();
            if (pts == null) return src;
            for (int i = 0; i < pts.Count; i++)
            {
                bool dup = false;
                for (int j = 0; j < src.Count; j++)
                {
                    if (Math.Abs(src[j].X - pts[i].X) < 1e-9
                        && Math.Abs(src[j].Y - pts[i].Y) < 1e-9)
                    {
                        dup = true;
                        break;
                    }
                }
                if (!dup) src.Add(pts[i]);
            }
            if (src.Count <= 3) return src;
            src.Sort((a, b) =>
            {
                int c = a.X.CompareTo(b.X);
                return c != 0 ? c : a.Y.CompareTo(b.Y);
            });
            var lower = new List<WellPt>();
            for (int i = 0; i < src.Count; i++)
            {
                while (lower.Count >= 2
                    && Cross(lower[lower.Count - 2], lower[lower.Count - 1], src[i]) <= 1e-12)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(src[i]);
            }
            var upper = new List<WellPt>();
            for (int i = src.Count - 1; i >= 0; i--)
            {
                while (upper.Count >= 2
                    && Cross(upper[upper.Count - 2], upper[upper.Count - 1], src[i]) <= 1e-12)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(src[i]);
            }
            if (lower.Count > 0) lower.RemoveAt(lower.Count - 1);
            if (upper.Count > 0) upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        static bool PipeStrip(WellPitInput w, WellPipeSeg p, double hwL, double hwR,
            out double ux, out double uy, out double lx, out double ly,
            out double cx, out double cy, out double R, out double z0,
            out WellPt L0, out WellPt R0, out WellPt farL, out WellPt farR,
            out WellPt C, out WellPt far)
        {
            ux = uy = lx = ly = cx = cy = R = z0 = 0.0;
            L0 = R0 = farL = farR = C = far = new WellPt();
            if (w == null || p == null) return false;
            if (hwL < 0.05) hwL = 0.05;
            if (hwR < 0.05) hwR = 0.05;
            double dx = p.X1 - w.X, dy = p.Y1 - w.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6)
            {
                dx = p.X1 - p.X0;
                dy = p.Y1 - p.Y0;
                len = Math.Sqrt(dx * dx + dy * dy);
            }
            if (len < 1e-6) return false;
            ux = dx / len;
            uy = dy / len;
            lx = -uy;
            ly = ux;
            cx = w.X;
            cy = w.Y;
            R = Radius(w);
            z0 = p.Invert0;
            L0 = new WellPt(cx + lx * hwL, cy + ly * hwL, z0);
            R0 = new WellPt(cx - lx * hwR, cy - ly * hwR, z0);
            farL = new WellPt(p.X1 + lx * hwL, p.Y1 + ly * hwL, z0);
            farR = new WellPt(p.X1 - lx * hwR, p.Y1 - ly * hwR, z0);
            C = new WellPt(cx, cy, z0);
            far = new WellPt(p.X1, p.Y1, z0);
            return true;
        }

        static bool PipeFrame(WellPitInput w, WellPipeSeg p, IList<WellPt> berm,
            double hwL, double hwR,
            out double ux, out double uy, out double lx, out double ly,
            out double cx, out double cy, out double R, out double z0,
            out WellPt bermL, out WellPt L0, out WellPt R0, out WellPt bermR, out WellPt C)
        {
            ux = uy = lx = ly = cx = cy = R = z0 = 0.0;
            bermL = L0 = R0 = bermR = C = new WellPt();
            if (w == null || p == null || berm == null || berm.Count < 3) return false;
            if (hwL < 0.05) hwL = 0.05;
            if (hwR < 0.05) hwR = 0.05;
            double dx = p.X1 - w.X, dy = p.Y1 - w.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6)
            {
                dx = p.X1 - p.X0;
                dy = p.Y1 - p.Y0;
                len = Math.Sqrt(dx * dx + dy * dy);
            }
            if (len < 1e-6) return false;
            ux = dx / len;
            uy = dy / len;
            lx = -uy;
            ly = ux;
            cx = w.X;
            cy = w.Y;
            R = Radius(w);
            z0 = p.Invert0;
            double bxL, byL, bxR, byR;
            if (!OffsetBermHit(berm, cx, cy, ux, uy, lx, ly, hwL, out bxL, out byL))
                return false;
            if (!OffsetBermHit(berm, cx, cy, ux, uy, -lx, -ly, hwR, out bxR, out byR))
                return false;
            L0 = new WellPt(cx + lx * hwL, cy + ly * hwL, z0);
            R0 = new WellPt(cx - lx * hwR, cy - ly * hwR, z0);
            C = new WellPt(cx, cy, z0);
            bermL = new WellPt(bxL, byL, z0);
            bermR = new WellPt(bxR, byR, z0);
            return true;
        }

        static WellPt CircleJoin(double cx, double cy, double z,
            double ux, double uy, double lx, double ly, double hw, double R, bool left)
        {
            double s = left ? 1.0 : -1.0;
            if (hw < R - 1e-9)
            {
                double t = Math.Sqrt(R * R - hw * hw);
                return new WellPt(cx + ux * t + s * lx * hw, cy + uy * t + s * ly * hw, z);
            }
            return new WellPt(cx + s * lx * R, cy + s * ly * R, z);
        }

        static bool OffsetBermHit(IList<WellPt> berm, double cx, double cy,
            double ux, double uy, double lx, double ly, double hw,
            out double x, out double y)
        {
            double x0 = cx + lx * hw, y0 = cy + ly * hw;
            double reach = 80.0;
            return ExitBerm(berm, x0, y0, x0 + ux * reach, y0 + uy * reach, out x, out y, out _);
        }

        static void AddArc(List<WellPt> pts, double cx, double cy, double z, double R,
            WellPt a, WellPt b, double towardX, double towardY)
        {
            double a0 = Math.Atan2(a.Y - cy, a.X - cx);
            double a1 = Math.Atan2(b.Y - cy, b.X - cx);
            double da = a1 - a0;
            while (da > Math.PI) da -= 2.0 * Math.PI;
            while (da < -Math.PI) da += 2.0 * Math.PI;
            double amid = a0 + 0.5 * da;
            double mx = Math.Cos(amid), my = Math.Sin(amid);
            if (mx * towardX + my * towardY < 0.0)
            {
                if (da >= 0.0) da -= 2.0 * Math.PI;
                else da += 2.0 * Math.PI;
            }
            int n = (int)Math.Ceiling(Math.Abs(da) / (Math.PI / 18.0));
            if (n < 2) n = 2;
            for (int i = 1; i < n; i++)
            {
                double ang = a0 + da * i / n;
                pts.Add(new WellPt(cx + R * Math.Cos(ang), cy + R * Math.Sin(ang), z));
            }
        }

        static bool ExitBerm(IList<WellPt> berm, double x0, double y0, double x1, double y1,
            out double x, out double y, out double tHit)
        {
            x = x0;
            y = y0;
            tHit = 1.0;
            bool any = false;
            int n = berm.Count;
            for (int i = 0; i < n; i++)
            {
                var a = berm[i];
                var b = berm[(i + 1) % n];
                double t, u;
                if (!SegSeg(x0, y0, x1, y1, a.X, a.Y, b.X, b.Y, out t, out u)) continue;
                if (t < 1e-6 || t > 1.0) continue;
                if (!any || t < tHit)
                {
                    tHit = t;
                    x = x0 + t * (x1 - x0);
                    y = y0 + t * (y1 - y0);
                    any = true;
                }
            }
            return any;
        }

        /// <summary>
        /// 2D разрез рядом с колодцем: t поперёк оси, h = (z − sump) * hScale,
        /// вставка сдвинута вдоль оси на alongHalf + pad.
        /// </summary>
        public static List<WellPt> SectionFlat(WellPitInput w, WellPitShape s, double hScale, double pad)
        {
            if (w == null || s == null || s.Section == null || s.Section.Count < 4)
                return new List<WellPt>();
            if (hScale <= 0.0) hScale = 1.0;
            double ax = s.Ax, ay = s.Ay, px = s.Px, py = s.Py;
            double shift = s.AlongHalf + Math.Max(0.5, pad);
            double ox = w.X + ax * shift;
            double oy = w.Y + ay * shift;
            double z0 = w.Sump;
            var pts = new List<WellPt>(s.Section.Count);
            for (int i = 0; i < s.Section.Count; i++)
            {
                var p = s.Section[i];
                double t = (p.X - w.X) * px + (p.Y - w.Y) * py;
                double h = (p.Z - z0) * hScale;
                pts.Add(new WellPt(ox + px * t + ax * h, oy + py * t + ay * h, 0.0));
            }
            return pts;
        }

        public static List<WellPt> ShaftFlat(WellPitInput w, WellPitShape s, double hScale, double pad)
        {
            if (w == null || s == null || s.Shaft == null || s.Shaft.Count < 4)
                return new List<WellPt>();
            if (hScale <= 0.0) hScale = 1.0;
            double ax = s.Ax, ay = s.Ay, px = s.Px, py = s.Py;
            double shift = s.AlongHalf + Math.Max(0.5, pad);
            double ox = w.X + ax * shift;
            double oy = w.Y + ay * shift;
            double z0 = w.Sump;
            var pts = new List<WellPt>(s.Shaft.Count);
            for (int i = 0; i < s.Shaft.Count; i++)
            {
                var p = s.Shaft[i];
                double t = (p.X - w.X) * px + (p.Y - w.Y) * py;
                double h = (p.Z - z0) * hScale;
                pts.Add(new WellPt(ox + px * t + ax * h, oy + py * t + ay * h, 0.0));
            }
            return pts;
        }
    }
}
