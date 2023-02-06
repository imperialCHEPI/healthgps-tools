# Health-GPS Tools

[Health-GPS](https://github.com/imperialCHEPI/healthgps) microsimulation supporting tools for input and output data processing, model parameters fitting, and visualisation.

*Health-GPS input* includes external user data models and files:
- [x] The risk factors model hierarchy definition, including range.
- [x] Dataset used for fitting the risk factor model parameters.
- [x] Fitted risk factor models' parameters files.
- [x] Baseline adjustments for risk factors.

*Health-GPS's backend data storage* provides reusable, reference datasets in standardised format for improved usability.
- [x] Countries definition ([ISO 3166](https://www.iso.org/iso-3166-country-codes.html)).
- [x] Demographics estimate and projections for countries.
- [x] Burden of diseases estimates for included diseases.
- [x] Standard diseases and cancers definitions.
- [x] Risk-factors and diseases interactions with diseases.

*Health-GPS outputs* two files:
- [x] Global average summary of the simulation experiment results in JSON (JavaScript Object Notation) format.
- [x] Detailed simulation results associated with simulation experiment in CSV (Comma Separated Values) format.

> **Note**  
> The results file can be large, depending on the size of experiment. These tools are provided to tabulate the results, create plots dataset, etc., and minimise the coping of large files over the network.

The main data sources being used by the Health-GPS model are:
- United Nations' [World Population Prospects](https://population.un.org/wpp/) 2019/2022 for countries demographic estimates, and projections by sex and age groups from 1950 until 2100.
- Institute for Health Metrics and Evaluation ([IHME](https://www.healthdata.org)) for diseases prevalence, incidence, mortality and remission rates for most diseases and countries.
- International Agency for Research on Cancer ([IARC](https://www.iarc.who.int)) provides prevalence at 1, 3 and 5 years, incidence, and mortality rates for cancers.
- Literature manually processed data, provided at custom datasets, e.g. risks and diseases interactions with diseases.

External scripts are included for documentation:
- Data input scripts using C# in [LINQPad](https://www.linqpad.net) for processing external data and converting regression models from the STOP prototype.
- Simulation results global summary (.json) processing using [R](https://www.r-project.org) for pivoting data, filtering and creating plotting.
