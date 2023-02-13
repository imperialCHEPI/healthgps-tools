# Health-GPS Tools

[Health-GPS](https://github.com/imperialCHEPI/healthgps) microsimulation supporting tools and scripts for processing inputs and outputs data, fitting model parameters, and generating common results data visualisation.

*Health-GPS inputs* in JSON (JavaScript Object Notation) format, contain external, user defined models and data, only known to the microsimulation at run-time. The following must be defined by the user and provided as input to the microsimulation:
- [x] Risk factors model's hierarchy definition, including range.
- [x] Dataset used for fitting the risk factor model parameters, CSV (Comma Separated Values) format.
- [x] Fitted hierarchy risk factors model' parameters files, (JSON) format.
- [x] Baseline adjustments for risk factors (mean by sex) files, CSV format.

*Health-GPS's backend data storage* provides reusable, curated datasets in standardised format for improved usability. The current *file-based storage* is indexed by a versioned JSON file (`index.json`), datasets are stored in standardised CSV format per country, using the country's ISO code as identifier. The following category of data are current stored:
- [x] Countries definition ([ISO 3166](https://www.iso.org/iso-3166-country-codes.html)), the central identifier dataset.
- [x] Demographics estimate and projections for countries by year, age and sex.
- [x] Burden of diseases estimates for included diseases by age and sex.
- [x] Standard diseases and cancers definitions for countries by age and sex.
- [x] Risk-factors and diseases interactions with diseases by age and sex.

*Health-GPS outputs* two files:
- [x] Global average summary of the simulation experiment results in JSON format.
- [x] Detailed simulation results associated with simulation experiment in CSV format.

> **Note**  
> The results file can be large (gigabyte), depending on the experiment size. The results should be processed close to source, e.g., HPC, to minimise the coping of large files over the network.

## Data Sources

The main data sources being used to populate the Health-GPS backend storage are:
- United Nations' [World Population Prospects](https://population.un.org/wpp/) for countries demographic estimates, projections, and indicators to age 100 by sex from 1950 to 2100. The current version being used by Health-GPS is 2022, the *CSV format* datasets, medium variant, single year.
- Institute for Health Metrics and Evaluation ([IHME](https://www.healthdata.org)) for diseases prevalence, incidence, mortality and remission rates for diseases by age groups and sex for most countries. The current version being used by Health-GPS is 2019, two different tools have been used to extract the data for multiple countries:
  1. [GBD Results](https://vizhub.healthdata.org/gbd-results/)  - single file per disease.
  2. [Epi Visualization](https://vizhub.healthdata.org/epi/) - multiple files per disease in one folder.
- International Agency for Research on Cancer ([IARC](https://www.iarc.who.int)) provides prevalence at 1, 3 and 5 years, incidence, and mortality rates for cancers.
- Literature manually processed data, provided at custom datasets, e.g. risks and diseases interactions with diseases.

## Scripts

Several scripts have been developed to processed external datasets, which format may change with versions, and simulation output files as follows:  

- [LINQPad](https://www.linqpad.net) scripts, written in C# are used for:
  1. Processing and reconcile external datasets into the standardised format required by the Health-GPS's data model.
  2. Converting risk factor models, distributed as part of the STOP prototype to Health-GPS required JSON format.

- [R](https://www.r-project.org) scripts are used for pivoting, filtering and plotting the Health-GPS results' global summary file (.json).

## Tools

Common tasks with stable workflows associated with inputs and output data processing have been automated using supporting tools, which fall into two categories: *Windows only* and *cross-platform*. The toolset provides applications with simply and consistent command-line interface (CLI), combining each ***command*** with an independent ***configuration*** file per task. This CLI pattern provides command isolation, sub-commands, validation and extensibility, e.g., to incorporate the scripts functionality in the future. 

### Health-GPS ModelFit

Fits predefined models to data and outputs the fitted parameters to a file to used by Health-GPS. This tool integrates [R](https://www.r-project.org) and uses the default R installation in the machine to fit the model to data. The application is written in C# targeting .NET Framework 4.8, and supports *Windows only* due to R integration constraints. The '[ica](https://cran.r-project.org/web/packages/ica/index.html)' package must be manually installed in R prior to running this application.

A single command: `risk-factor` is current provided to fit a *hierarchical risk factors model* to data. The command outputs the fitted parameters for both the *static* and *dynamic* model versions. A *configuration* file example (`France.RiskFactor.json`) is shown below:

```JSON
{
  "dataset": {
    "filename": "France.DataFile.csv",
    "format": "csv",
    "delimiter": ","
  },
  "modelling": {
    "risk_factors": {
      "Year": 0,
      "Gender": 0,
      "Age": 0,
      "Age2": 0,
      "Age3": 0,
      "SES": 0,
      "Sodium": 1,
      "Protein": 1,
      "Fat": 1,
      "PA": 2,
      "Energy": 2,
      "BMI": 3
    },
    "dynamic_risk_factor": "Year",
    "models": {
      "static": {
        "filename": "France.HLM.Json",
        "include_dynamic_factor": false
      },
      "dynamic": {
        "filename": "France.DHLM.Json",
        "include_dynamic_factor": true
      }
    }
  }
}
```

The associated *dataset* file must be in the same folder as the *configuration* file, and contain all factors (columns) used to define the risk factor model's hierarchy. The command line to execute the action is shown bellow:

```bash
> HealthGps.ModelFit risk-factor --file Example\\France.RiskFactor.json
```

>**Warning**  
> The regression residuals are included in the fitted parameter files, therefore, the size is proportional to that of the dataset.

Both output files are written to the same folder containing the configuration file. Current, only the *static* model is used by Health-GPS to initialise the population risk factors. The *dynamic* model is create elsewhere and converted to Health-GPS format using script.


### Health-GPS Tools

This tool requires .NET 6.0 or newer, to build the application for running on *Imperial HPC*, use the following *command* from inside the *Tools*' project folder:

```bash
> dotnet publish -r linux-x64 -c Release --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRunComposite=true -p:EnableCompressionInSingleFile=true
```

This will create a *single executable* file, which you can copy and run on the HPC by itself without any installation requirement.

A single command: output is current provided to process the various outputs and log files produced by health-GPS batch mode experiments. The command outputs the processed files in the same folder. A configuration file example ([output_settings.json](https://github.com/imperialCHEPI/healthgps-tools/blob/main/HealthGPS.Tools/Config/outputsettings.json)) is provided in the source code. The command line to execute the action is shown bellow:

```bash
> HealthGps.Tools output --file Config\\output_settings.json
```
