using RDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HealthGps.R
{
    /// <summary>
    /// Implements the R stats provider class.
    /// </summary>
    /// <remarks>
    /// This provided requires the <code>ica</code> package to 
    /// installed manually in the R prior to using this library.
    /// </remarks>
    public sealed class RStatsProvider : IStatsProvider
    {
        /// <summary>
        /// Holds the shared R engine instance
        /// </summary>
        private readonly REngine provider;

        /// <summary>
        /// Initialises the <see cref="RStatsProvider"/> class with a custom installed version.
        /// </summary>
        /// <param name="rbinFolder">The R installation to be used's bin folder</param>
        public RStatsProvider(DirectoryInfo rbinFolder)
        {
            if (rbinFolder != null)
            {
                REngine.SetEnvironmentVariables(rbinFolder.FullName);
            }

            provider = REngine.GetInstance();

            // This might not be necessary.
            provider.Initialize();
            if (provider.IsRunning == false)
            {
                provider.Initialize();
            }
        }

        /// <summary>
        /// Initialises the <see cref="RStatsProvider"/> class with default installed version.
        /// </summary>
        public RStatsProvider()
            : this(null)
        {
        }

        /// <inheritdoc/>
        public KernelDensity Density(ICollection<double> data)
        {
            try
            {
                var nv = provider.CreateNumericVector(data);
                provider.SetSymbol("data", nv);

                var density = provider.Evaluate("d = density(data)");
                var fields = density.AsList();
                return new KernelDensity
                {
                    X = fields["x"].AsNumeric().ToArray(),
                    Y = fields["y"].AsNumeric().ToArray(),
                    Bandwidth = fields["bw"].AsNumeric().FirstOrDefault(),
                    N = fields["n"].AsInteger().FirstOrDefault(),
                };
            }
            finally
            {
                ClearObjects();
            }
        }

        /// <inheritdoc/>
        public LinearModelResult FitLinearModel(string dependent,
            IReadOnlyCollection<string> predictors,
            IReadOnlyDictionary<string, List<double>> data)
        {
            LinearModelResult model;
            try
            {
                // Populate data before calling the internal fit function.
                foreach (var item in data)
                {
                    provider.SetSymbol(item.Key, provider.CreateNumericVector(item.Value));
                }

                model = InternalFitLinearModel(dependent, predictors);
            }
            finally
            {
                ClearObjects();
            }

            return model;
        }

        /// <inheritdoc/>
        public HierarchicalModelResult FitHierarchicalModel(
            IReadOnlyDictionary<string, int> modelLevels,
            IReadOnlyDictionary<string, List<double>> data,
            IProgress<double> progress = null)
        {
            HierarchicalModelResult model;
            try
            {
                // Make sure external package are installed
                progress?.Report(0.0);
                provider.Evaluate("require(ica)");
                provider.Evaluate("library(ica)");

                // Populate data before calling the internal fit function.
                foreach (var item in data)
                {
                    provider.SetSymbol(item.Key, provider.CreateNumericVector(item.Value));
                }

                progress?.Report(5.0);
                var uniqueLvels = modelLevels.Select(s => s.Value)
                                        .Distinct().OrderBy(o => o)
                                        .ToList();

                var models = new Dictionary<string, LinearModelResult>(modelLevels.Count);
                var levels = new Dictionary<int, HierarchicalLevelResult>(uniqueLvels.Count);

                var levelAvg = (1.0 / uniqueLvels.Count) * 100.0;
                foreach (var level in uniqueLvels)
                {
                    if (level == 0)
                    {
                        continue;
                    }

                    var dependents = modelLevels.Where(s => s.Value == level)
                                                .Select(s => s.Key)
                                                .ToList();

                    var predictors = modelLevels.Where(s => s.Value < level)
                                                .Select(s => s.Key)
                                                .ToList();

                    foreach (var dependent in dependents)
                    {
                        models.Add(
                            dependent,
                            InternalFitLinearModel(dependent, predictors));
                    }

                    levels.Add(
                        level,
                        InternalFitIndependentComponents(dependents, models));

                    progress?.Report(Math.Round(level * levelAvg, 2));
                }

                progress?.Report(100.0);
                model = new HierarchicalModelResult
                {
                    Models = models,
                    Levels = levels,
                };
            }
            finally
            {
                ClearObjects();
            }

            return model;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            provider?.Dispose();
        }

        /// <summary>
        /// Clear all object from R workspace.
        /// </summary>
        private void ClearObjects()
        {
            provider.Evaluate("rm(list = ls())");
        }

        /// <summary>
        /// Fits a regression linear model to the loaded data.
        /// </summary>
        /// <param name="dependent">The dependent factor. </param>
        /// <param name="predictors">The predictor factor. </param>
        /// <returns>The fitted linear model results. </returns>
        private LinearModelResult InternalFitLinearModel(string dependent, IReadOnlyCollection<string> predictors)
        {
            var equation = new StringBuilder("result = lm(" + dependent + "~");
            var firstPredictor = true;
            foreach (var predictor in predictors)
            {
                if (firstPredictor)
                {
                    firstPredictor = false;
                }
                else
                {
                    equation.Append("+");
                }

                equation.Append(predictor);
            }

            equation.Append(",na.action=na.exclude)");

            var result = provider.Evaluate(equation.ToString());
            var summary = provider.Evaluate("summary(result)");

            var lmData = result.AsList();
            var lmInfo = summary.AsList();
            var model = new LinearModelResult
            {
                Formula = equation.ToString(),
                Residuals = lmData["residuals"].AsNumeric().ToList(),
                FittedValues = lmData["fitted.values"].AsNumeric().ToList(),
                RSquared = lmInfo["r.squared"].AsNumeric().FirstOrDefault(),
            };

            model.ResidualsStandardDeviation = CalculateStandardDeviation(model.Residuals);

            var offset = predictors.Count + 1;
            var coefMat = lmInfo["coefficients"].AsNumericMatrix();
            var coefficients = new Dictionary<string, Coefficient>(offset);
            for (var i = 0; i < coefMat.RowCount; i++)
            {
                var key = coefMat.RowNames[i];
                if (i == 0)
                {
                    key = key.Replace("(", string.Empty).Replace(")", string.Empty);
                }

                coefficients.Add(key, new Coefficient
                {
                    Value = coefMat[i, 0],
                    StdError = coefMat[i, 1],
                    TValue = coefMat[i, 2],
                    PValue = coefMat[i, 3]
                });
            }

            model.Coefficients = coefficients;

            return model;
        }

        /// <summary>
        /// Independent components analysis of a hierarchy level linear models.
        /// </summary>
        /// <param name="dependents">Dependent factors collection.</param>
        /// <param name="models">Fitted linear model results collection. </param>
        /// <returns>The level independent components analysis results. </returns>
        private HierarchicalLevelResult InternalFitIndependentComponents(
            IReadOnlyCollection<string> dependents,
            IReadOnlyDictionary<string, LinearModelResult> models)
        {
            var dataFrame = new StringBuilder("residuals = data.frame(");
            var isFirstVariable = true;
            foreach (var key in dependents)
            {
                // Must not overwriting the risk factors variables.
                var model_key = $"{key}_model";

                var dependentVariable =
                    provider.CreateNumericVector(models[key].Residuals);

                provider.SetSymbol(model_key, dependentVariable);
                if (isFirstVariable)
                {
                    dataFrame.Append(model_key);
                    isFirstVariable = false;
                }
                else
                {
                    dataFrame.Append($",{model_key}");
                }
            }

            dataFrame.Append(")");
            provider.Evaluate(dataFrame.ToString());

            var n = dependents.Count;
            var components = provider.Evaluate($"icafast(residuals, center = TRUE, nc ={n})");
            var corMatrix = provider.Evaluate("cor(residuals)");

            var compList = components.AsList();
            var m = models.First().Value.Residuals.Count;

            var smat = compList["S"].AsNumericMatrix();
            var colums = new Dictionary<int, List<double>>();
            for (int j = 0; j < smat.ColumnCount; j++)
            {
                colums.Add(j, new List<double>(smat.RowCount));
            }

            for (int i = 0; i < smat.RowCount; i++)
            {
                for (int j = 0; j < smat.ColumnCount; j++)
                {
                    colums[j].Add(smat[i, j]);
                }
            }

            var averages = new List<double>(smat.ColumnCount);
            var stdDevs = new List<double>(smat.ColumnCount);

            for (int j = 0; j < smat.ColumnCount; j++)
            {
                averages.Add(colums[j].Average());
                stdDevs.Add(CalculateStandardDeviation(colums[j]));
            }

            return new HierarchicalLevelResult()
            {
                Variables = dependents.ToList(),
                S = new Array2D<double>(m, n, compList["S"].AsNumericMatrix().ToArray()),
                M = new Array2D<double>(n, n, compList["M"].AsNumericMatrix().ToArray()),
                W = new Array2D<double>(n, n, compList["W"].AsNumericMatrix().ToArray()),
                Variances = new List<double>(compList["vafs"].AsNumeric()),
                Correlation = new Array2D<double>(n, n, corMatrix.AsNumericMatrix().ToArray()),
            };
        }

        /// <summary>
        /// Calculates the standard deviation of a dataset's factor
        /// </summary>
        /// <param name="values">The factor values </param>
        /// <returns>The standard deviation value. </returns>
        public double CalculateStandardDeviation(IEnumerable<double> values)
        {
            var sum1 = 0.0;
            var sum2 = 0.0;
            int count = 0;
            foreach (var residual in values)
            {
                sum1 += residual;
                sum2 += residual * residual;
                count++;
            }

            var m1 = sum1 / count;
            var m2 = sum2 / count;
            return Math.Sqrt(m2 - m1 * m1);
        }
    }
}
