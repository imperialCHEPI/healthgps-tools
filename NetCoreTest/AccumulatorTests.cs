using HealthGPS.Tools.Output;

namespace HealthGps.NetCoreTest;

[TestClass]
public class AccumulatorTests
{
    private static readonly double tol = 0.000001;

    [TestMethod]
    public void CreateEmptySummation()
    {
        var entity = new SummationValue();
        Assert.AreEqual(0, entity.Count);
        Assert.AreEqual(0.0, entity.Sum);
        Assert.AreEqual(0.0, entity.SumSq);
        Assert.AreEqual(double.NaN, entity.Variance());
        Assert.AreEqual(double.NaN, entity.StDev);
    }

    [TestMethod]
    public void CreateSimpleSummation()
    {
        var entity = new SummationValue();
        var data = new List<double> { 5.0, 4.5, 3.5, 7.13, 9.87 };
        foreach (var value in data)
        {
            entity.Add(1, value);
        }

        Assert.AreEqual(data.Count, entity.Count);
        Assert.AreEqual(30.0, entity.Sum);
        Assert.AreEqual(205.7538, entity.SumSq, tol);
        Assert.AreEqual(5.15076, entity.Variance(), tol);
        Assert.AreEqual(2.269528585, entity.StDev, tol);
    }
}
