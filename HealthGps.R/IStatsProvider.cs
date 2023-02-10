using System;
using System.Collections.Generic;

namespace HealthGps.R
{
    /// <summary>
    /// Defines the interfaces for statistical providers
    /// </summary>
    public interface IStatsProvider : IDisposable
    {
        /// <summary> Computes kernel density estimates. </summary>
        /// <param name="data">Data to estimate is to be computed.</param>
        /// <returns> The estimated kernel density data. </returns>
        KernelDensity Density(ICollection<double> data);

        /// <summary>
        /// Fit a single linear regression model to data
        /// </summary>
        /// <param name="dependent">The dependent factor in the dataset</param>
        /// <param name="predictors">The predictor factor in the dataset</param>
        /// <param name="data">The dataset to fit the model</param>
        /// <returns>The linear model fitted results.</returns>
        LinearModelResult FitLinearModel(string dependent,
            IReadOnlyCollection<string> predictors,
            IReadOnlyDictionary<string, List<double>> data);

        /// <summary>
        /// Fits a hierarchy of linear regression models to data
        /// </summary>
        /// <param name="modelLevels">The hierarchy levels</param>
        /// <param name="data">The dataset to fit the model</param>
        /// <param name="progress">The progress notification output.</param>
        /// <returns>The models hierarchy fitted results.</returns>
        HierarchicalModelResult FitHierarchicalModel(
            IReadOnlyDictionary<string, int> modelLevels,
            IReadOnlyDictionary<string, List<double>> data,
            IProgress<double> progress = null);
    }
}
