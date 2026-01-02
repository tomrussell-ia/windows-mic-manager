using System;

namespace MicrophoneManager.Services;

/// <summary>
/// OBS-style meter mapping for the LOG fader type.
/// Based on OBS Studio libobs/obs-audio-controls.c (log_db_to_def/log_def_to_db).
/// </summary>
internal static class ObsMeterMath
{
    // From OBS:
    // #define LOG_OFFSET_DB 6.0f
    // #define LOG_RANGE_DB 96.0f
    // #define LOG_OFFSET_VAL -0.77815125038364363f
    // #define LOG_RANGE_VAL -2.00860017176191756f
    private const double LogOffsetDb = 6.0;
    private const double LogRangeDb = 96.0;
    private const double LogOffsetVal = -0.77815125038364363;
    private const double LogRangeVal = -2.00860017176191756;

    /// <summary>
    /// Converts linear amplitude multiplier (0..1) to dBFS.
    /// </summary>
    public static double MulToDb(double mul)
    {
        if (mul <= 0.0) return double.NegativeInfinity;
        return 20.0 * Math.Log10(Math.Max(mul, 1e-20));
    }

    /// <summary>
    /// Converts dBFS to OBS LOG deflection (0..1).
    /// </summary>
    public static double DbToDeflection(double db)
    {
        if (db >= 0.0) return 1.0;
        if (db <= -LogRangeDb) return 0.0;

        return (-Math.Log10(-db + LogOffsetDb) - LogRangeVal) / (LogOffsetVal - LogRangeVal);
    }

    /// <summary>
    /// Converts OBS LOG deflection (0..1) back to dBFS.
    /// </summary>
    public static double DeflectionToDb(double deflection)
    {
        if (deflection >= 1.0) return 0.0;
        if (deflection <= 0.0) return double.NegativeInfinity;

        var ratio = (LogRangeDb + LogOffsetDb) / LogOffsetDb;
        return -(LogRangeDb + LogOffsetDb) * Math.Pow(ratio, -deflection) + LogOffsetDb;
    }

    public static double ClampMeterDb(double db)
    {
        if (double.IsNegativeInfinity(db) || double.IsNaN(db)) return -LogRangeDb;
        if (double.IsPositiveInfinity(db)) return 0.0;
        return Math.Max(-LogRangeDb, Math.Min(0.0, db));
    }

    public static double DbToPercent(double db)
        => Math.Clamp(DbToDeflection(db) * 100.0, 0.0, 100.0);

    public static double PercentToDb(double percent)
        => ClampMeterDb(DeflectionToDb(Math.Clamp(percent, 0.0, 100.0) / 100.0));
}
