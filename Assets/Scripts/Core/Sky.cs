using System;

namespace Height1079.Core
{
    /// <summary>Sky of the night 1–2 February 1959 over Kholat Syakhl: game clock → Julian day, sun and moon positions,
    /// local sidereal time and the equatorial→horizontal rotation for the star field. Local time of Sverdlovsk oblast in 1959 = UTC+5.
    /// Sun: NOAA solar position algorithm (≈0.01°). Moon: P. Schlyter's method with the main perturbations and topocentric parallax (≈0.3°).
    /// Checked against PyEphem in the tests (sunset 16:58, end of astronomical dusk 19:41, moonrise 04:21 on 2 Feb, 41 % illuminated).</summary>
    public static class Sky
    {
        public const double Latitude = 61.7586, Longitude = 59.4297, UtcOffsetHours = 5;
        /// <summary>Julian day of 1959-02-01 00:00 UTC.</summary>
        public const double JdFeb1 = 2436600.5;
        /// <summary>Galactic pole and centre as unit vectors in the equatorial frame of 1959 (x → RA 0h, z → north pole).</summary>
        public static readonly (double x, double y, double z) GalacticPole = (-0.86762, -0.19014, 0.45943), GalacticCentre = (-0.06479, -0.87289, -0.4836);

        const double D = Math.PI / 180;

        /// <summary>Local clock of the game in minutes after midnight of 1 Feb (may exceed 1440 after midnight).</summary>
        public static double ClockMinutes(float elapsed) => 17 * 60 + 40 + elapsed / SurvivalRules.NightSeconds * 790.0;

        public static double JulianDay(double localMinutes) => JdFeb1 + (localMinutes / 60.0 - UtcOffsetHours) / 24.0;

        static double Rev(double x) => x - Math.Floor(x / 360.0) * 360.0;

        /// <summary>Local mean sidereal time, degrees.</summary>
        public static double SiderealDeg(double jd)
        {
            double t = (jd - 2451545.0) / 36525.0;
            double gmst = 280.46061837 + 360.98564736629 * (jd - 2451545.0) + 0.000387933 * t * t - t * t * t / 38710000.0;
            return Rev(gmst + Longitude);
        }

        /// <summary>Altitude and azimuth (degrees, azimuth from north through east) of a body at RA/Dec (degrees, of date).</summary>
        public static (double alt, double az) Horizontal(double raDeg, double decDeg, double jd)
        {
            double h = (SiderealDeg(jd) - raDeg) * D, dec = decDeg * D, lat = Latitude * D;
            double sinAlt = Math.Sin(dec) * Math.Sin(lat) + Math.Cos(dec) * Math.Cos(lat) * Math.Cos(h);
            double alt = Math.Asin(sinAlt);
            double az = Math.Atan2(-Math.Cos(dec) * Math.Sin(h), Math.Sin(dec) * Math.Cos(lat) - Math.Cos(dec) * Math.Sin(lat) * Math.Cos(h));
            return (alt / D, Rev(az / D));
        }

        /// <summary>Sun: right ascension and declination (degrees, of date).</summary>
        public static (double ra, double dec) SunEquatorial(double jd)
        {
            double t = (jd - 2451545.0) / 36525.0;
            double l0 = Rev(280.46646 + t * (36000.76983 + t * 0.0003032));
            double m = Rev(357.52911 + t * (35999.05029 - 0.0001537 * t));
            double c = Math.Sin(m * D) * (1.914602 - t * (0.004817 + 0.000014 * t)) + Math.Sin(2 * m * D) * (0.019993 - 0.000101 * t) + Math.Sin(3 * m * D) * 0.000289;
            double omega = 125.04 - 1934.136 * t;
            double lambda = l0 + c - 0.00569 - 0.00478 * Math.Sin(omega * D);
            double eps = 23.0 + (26.0 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60.0) / 60.0 + 0.00256 * Math.Cos(omega * D);
            double ra = Math.Atan2(Math.Cos(eps * D) * Math.Sin(lambda * D), Math.Cos(lambda * D));
            double dec = Math.Asin(Math.Sin(eps * D) * Math.Sin(lambda * D));
            return (Rev(ra / D), dec / D);
        }

        public static (double alt, double az) Sun(double jd)
        {
            var (ra, dec) = SunEquatorial(jd);
            return Horizontal(ra, dec, jd);
        }

        /// <summary>Moon: topocentric altitude/azimuth (degrees) and illuminated fraction (0..1).</summary>
        public static (double alt, double az, double illuminated) Moon(double jd)
        {
            double d = jd - 2451543.5; // Schlyter's day number
            double N = Rev(125.1228 - 0.0529538083 * d), i = 5.1454, w = Rev(318.0634 + 0.1643573223 * d);
            double a = 60.2666, e = 0.054900, M = Rev(115.3654 + 13.0649929509 * d);
            double E = M + e / D * Math.Sin(M * D) * (1 + e * Math.Cos(M * D));
            for (int k = 0; k < 5; k++) E = E - (E - e / D * Math.Sin(E * D) - M) / (1 - e * Math.Cos(E * D));
            double xv = a * (Math.Cos(E * D) - e), yv = a * Math.Sqrt(1 - e * e) * Math.Sin(E * D);
            double v = Math.Atan2(yv, xv) / D, r = Math.Sqrt(xv * xv + yv * yv);
            double xh = r * (Math.Cos(N * D) * Math.Cos((v + w) * D) - Math.Sin(N * D) * Math.Sin((v + w) * D) * Math.Cos(i * D));
            double yh = r * (Math.Sin(N * D) * Math.Cos((v + w) * D) + Math.Cos(N * D) * Math.Sin((v + w) * D) * Math.Cos(i * D));
            double zh = r * Math.Sin((v + w) * D) * Math.Sin(i * D);
            double lon = Math.Atan2(yh, xh) / D, lat = Math.Atan2(zh, Math.Sqrt(xh * xh + yh * yh)) / D;

            // perturbations
            double Ms = Rev(356.0470 + 0.9856002585 * d), ws = 282.9404 + 4.70935e-5 * d;
            double Ls = Rev(Ms + ws), Lm = Rev(N + w + M), Dm = Rev(Lm - Ls), F = Rev(Lm - N);
            lon += -1.274 * Math.Sin((M - 2 * Dm) * D) + 0.658 * Math.Sin(2 * Dm * D) - 0.186 * Math.Sin(Ms * D)
                   - 0.059 * Math.Sin((2 * M - 2 * Dm) * D) - 0.057 * Math.Sin((M - 2 * Dm + Ms) * D) + 0.053 * Math.Sin((M + 2 * Dm) * D)
                   + 0.046 * Math.Sin((2 * Dm - Ms) * D) + 0.041 * Math.Sin((M - Ms) * D) - 0.035 * Math.Sin(Dm * D)
                   - 0.031 * Math.Sin((M + Ms) * D) - 0.015 * Math.Sin((2 * F - 2 * Dm) * D) + 0.011 * Math.Sin((M - 4 * Dm) * D);
            lat += -0.173 * Math.Sin((F - 2 * Dm) * D) - 0.055 * Math.Sin((M - F - 2 * Dm) * D) - 0.046 * Math.Sin((M + F - 2 * Dm) * D)
                   + 0.033 * Math.Sin((F + 2 * Dm) * D) + 0.017 * Math.Sin((2 * M + F) * D);
            r += -0.58 * Math.Cos((M - 2 * Dm) * D) - 0.46 * Math.Cos(2 * Dm * D);

            // ecliptic of date → equatorial
            double ecl = 23.4393 - 3.563e-7 * d;
            double xg = Math.Cos(lon * D) * Math.Cos(lat * D), yg = Math.Sin(lon * D) * Math.Cos(lat * D), zg = Math.Sin(lat * D);
            double xe = xg, ye = yg * Math.Cos(ecl * D) - zg * Math.Sin(ecl * D), ze = yg * Math.Sin(ecl * D) + zg * Math.Cos(ecl * D);
            double ra = Rev(Math.Atan2(ye, xe) / D), dec = Math.Atan2(ze, Math.Sqrt(xe * xe + ye * ye)) / D;

            var (alt, az) = Horizontal(ra, dec, jd);
            double par = Math.Asin(1 / r) / D;             // horizontal parallax
            alt -= par * Math.Cos(alt * D);

            // phase from the elongation to the sun
            var (sra, sdec) = SunEquatorial(jd);
            double cosElong = Math.Sin(sdec * D) * Math.Sin(dec * D) + Math.Cos(sdec * D) * Math.Cos(dec * D) * Math.Cos((sra - ra) * D);
            double illum = (1 - cosElong) / 2;
            return (alt, az, illum);
        }

        /// <summary>Unit vector in the game frame (x east, y up, z north) for altitude/azimuth in degrees.</summary>
        public static (double x, double y, double z) Direction(double altDeg, double azDeg)
        {
            double alt = altDeg * D, az = azDeg * D;
            return (Math.Cos(alt) * Math.Sin(az), Math.Sin(alt), Math.Cos(alt) * Math.Cos(az));
        }

        /// <summary>Columns of the rotation that takes an equatorial unit vector (x → RA 0h, z → pole) to the game frame at <paramref name="jd"/>.</summary>
        public static ((double x, double y, double z) c0, (double x, double y, double z) c1, (double x, double y, double z) c2) CelestialRotation(double jd)
        {
            double lst = SiderealDeg(jd) * D, lat = Latitude * D;
            var m = (x: 0.0, y: Math.Cos(lat), z: -Math.Sin(lat));   // equator on the meridian (south)
            var w = (x: -1.0, y: 0.0, z: 0.0);                        // equator at hour angle +6h (west)
            var p = (x: 0.0, y: Math.Sin(lat), z: Math.Cos(lat));    // celestial pole
            double cl = Math.Cos(lst), sl = Math.Sin(lst);
            var c0 = (cl * m.x + sl * w.x, cl * m.y + sl * w.y, cl * m.z + sl * w.z);
            var c1 = (sl * m.x - cl * w.x, sl * m.y - cl * w.y, sl * m.z - cl * w.z);
            return (c0, c1, p);
        }

        public static (double x, double y, double z) Rotate(((double x, double y, double z) c0, (double x, double y, double z) c1, (double x, double y, double z) c2) r, (double x, double y, double z) v)
            => (r.c0.x * v.x + r.c1.x * v.y + r.c2.x * v.z, r.c0.y * v.x + r.c1.y * v.y + r.c2.y * v.z, r.c0.z * v.x + r.c1.z * v.y + r.c2.z * v.z);
    }
}
