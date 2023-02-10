using HealthGPS.Tools;

namespace HealthGps.NetCoreTest;

[TestClass]
public class FactorMomentTests
{
    private static readonly double tolerance = 0.00001;
    private static readonly double[] data = new double[]
    {
        5.07135, 4.42195, 3.93045, 5.13533, 5.4797,
        4.75258, 4.85492, 5.37211, 4.95738, 4.58651
    };

    private static void AssertInitialMoment(SampleMoment moment)
    {
        Assert.AreEqual(0, moment.Count);
        Assert.AreEqual(0.0, moment.Sum);
        Assert.AreEqual(double.NaN, moment.Minimum);
        Assert.AreEqual(double.NaN, moment.Maximum);
        Assert.AreEqual(double.NaN, moment.Range);
        Assert.AreEqual(0.0, moment.Average);
        Assert.AreEqual(double.NaN, moment.Variance);
        Assert.AreEqual(double.NaN, moment.StdDeviation);
        Assert.AreEqual(double.NaN, moment.StdError);
        Assert.AreEqual(double.NaN, moment.Kurtosis);
        Assert.AreEqual(double.NaN, moment.Skewness);
    }

    private static void AssertExcelMoment(SampleMoment moment)
    {
        Assert.AreEqual(data.Length, moment.Count);
        Assert.AreEqual(48.56228, moment.Sum, tolerance);
        Assert.AreEqual(3.93045, moment.Minimum, tolerance);
        Assert.AreEqual(5.4797, moment.Maximum, tolerance);
        Assert.AreEqual(1.54925, moment.Range, tolerance);
        Assert.AreEqual(4.856228, moment.Average, tolerance);
        Assert.AreEqual(0.213157045106667, moment.Variance, tolerance);
        Assert.AreEqual(0.461689338307337, moment.StdDeviation, tolerance);
        Assert.AreEqual(0.145998988046721, moment.StdError, tolerance);
        Assert.AreEqual(0.492033123594497, moment.Kurtosis, tolerance);
        Assert.AreEqual(-0.682658375723596, moment.Skewness, tolerance);
    }

    [TestMethod]
    public void CreateEmpty()
    {
        var moment = new SampleMoment();
        AssertInitialMoment(moment);
    }

    [TestMethod]
    public void AddSingle()
    {
        var moment = new SampleMoment();
        foreach (var value in data)
        {
            moment.Add(value);
        }

        AssertExcelMoment(moment);
    }

    [TestMethod]
    public void AddRange()
    {
        var moment = new SampleMoment();
        moment.AddRange(data);
        AssertExcelMoment(moment);
    }

    [TestMethod]
    public void ClearToEmpty()
    {
        var moment = new SampleMoment();
        moment.Add(3.0);
        moment.Add(5.0);
        moment.Add(7.0);
        moment.Add(9.0);

        Assert.AreEqual(4, moment.Count);
        Assert.AreEqual(24.0, moment.Sum);
        Assert.AreEqual(3.0, moment.Minimum);
        Assert.AreEqual(9.0, moment.Maximum);
        Assert.AreEqual(6.0, moment.Average);

        moment.Clear();
        AssertInitialMoment(moment);
    }
}