require(jsonlite);

plot_xy <- function(x, y, title='', xlabel='Time (Year)', ylabel='', colour = 'blue')
{
  plot(x, y, type='l', xlab= xlabel, ylab = ylabel, col = colour)
  title(title)
}

plot_gender <- function(x, y1, y2, title='', xlabel='Time (Years)', ylabel='', legendxy = 'topright')
{
  ymin = min(y1, min(y2))
  ymax = max(y1, max(y2))
  
  plot(x, y1, type='l', xlab= xlabel, ylab = ylabel, ylim=c(ymin, ymax), col = 'blue')
  lines(x, y2, type='l', col = 'red')
  
  title(title)
  legend(legendxy, legend = c('Male', 'Female'), lty = c(1, 1), col = c('blue', 'red'), lwd = 2)  
}

get_latest_result_file <- function(output_folder){
  df <- file.info(list.files(output_folder, pattern = "json", ignore.case = T, full.names = T))
  last_result_filename = rownames(df)[which.max(df$mtime)]
  return(last_result_filename)
}

# Finds the latest result file (json format)
output_folder = 'C:/HealthGPS/Result';
last_result_filename = get_latest_result_file(output_folder)
stopifnot(nchar(last_result_filename) >= 2)

# Load json data into r struct
print(paste("Loading results file: ", last_result_filename))
data = fromJSON(last_result_filename)
x = unique(data$result$time)
nruns = length(unique(data$result$run))
nscenarios = length(unique(data$result$source))

baseline = which(data$result$source == "baseline")
policy = which(data$result$source == "intervention")

# Clear existing plots
graphics.off()

# Population
if (F) {
  par(mfrow=c(2,2))
  plot_xy(x, data$result$population$size, title ='Population Total', ylabel = 'Size');
  plot_xy(x, data$result$population$alive, title ='Population Alive', ylabel = 'Size');
  plot_xy(x, data$result$population$dead, title ='Population Dead', ylabel = 'Size');
  plot_xy(x, data$result$population$migrating, title ='Population Migrating', ylabel = 'Size');
}

# Indicators
if (F) {
  par(mfrow=c(3,1))
  plot_xy(x, data$result$indicators$YLL, title ='Year of Life Lost', ylabel = 'YLL');
  plot_xy(x, data$result$indicators$YLD, title ='Year Lived with Disability', ylabel = 'YLD')
  plot_xy(x, data$result$indicators$DALY, title ='Disability-Adjusted Live Years', ylabel = 'DALY')
}

# Metrics
if (F){
  par(mfrow=c(3,1))
  plot_xy(x, data$result$metrics$ExpectedDeathRate, title ='Expected Death Rate', ylabel = 'x1000')
  plot_xy(x, data$result$metrics$SimulatedDeathRate, title ='Simulated Death Rate', ylabel = 'x1000')
  plot_xy(x, data$result$metrics$DeathRateDeltaPercent, title ='Death Rate Delta', ylabel = 'Percent')
}

# Average Age
if (F) {
  par(mfrow=c(1,1))
  plot_gender(x, data$result$average_age$male, data$result$average_age$female,
             title = 'Population Age', ylabel = 'Average', legendxy = "topleft")
}

# Diseases
if (F) {
  par(mfrow=c(2,1))
  plot_gender(x, data$result$disease_prevalence$asthma$male, data$result$disease_prevalence$asthma$female,
            title = 'Asthma projection', ylabel = 'Prevalence (%)', legendxy = "center")

  plot_gender(x, data$result$disease_prevalence$diabetes$male, data$result$disease_prevalence$diabetes$female,
            title = 'Diabetes projection', ylabel = 'Prevalence (%)', legendxy = "center")
}

# Risk Factors
if (F){
  par(mfrow=c(2,2))
  plot_gender(x, data$result$risk_factors_average$AlcoholConsumption$male, data$result$risk_factors_average$AlcoholConsumption$female,
              title = 'AlcoholConsumption', ylabel = 'Average', legendxy = "topleft")
  
  plot_gender(x, data$result$risk_factors_average$PhysicalActivityMET$male, data$result$risk_factors_average$PhysicalActivityMET$female,
              title = 'PhysicalActivityMET', ylabel = 'Average')
  
  plot_gender(x, data$result$risk_factors_average$FruitsVegetablesConsumption$male, data$result$risk_factors_average$FruitsVegetablesConsumption$female,
              title = 'FruitsVegetablesConsumption', ylabel = 'Average', legendxy = "topleft")
  
  plot_gender(x, data$result$risk_factors_average$SmokingStatus$male, data$result$risk_factors_average$SmokingStatus$female,
              title = 'SmokingStatus', ylabel = 'Average')
}

if (T){
  if (nscenarios > 1) {
    par(mfrow=c(1,2))
    y_male = data$result$risk_factors_average$BMI$male[baseline]
    y_female = data$result$risk_factors_average$BMI$female[baseline]
    plot_gender(x, y_male, y_female, title = 'BMI - Baseline', ylabel = 'Average', legendxy = "left")

    y_male = data$result$risk_factors_average$BMI$male[policy]
    y_female = data$result$risk_factors_average$BMI$female[policy]
    plot_gender(x, y_male, y_female, title = 'BMI - Intervention', ylabel = 'Average', legendxy = "left")
  } 
  else {
    par(mfrow=c(1,1))
    y_male = data$result$risk_factors_average$BMI$male[baseline]
    y_female = data$result$risk_factors_average$BMI$female[baseline]
    plot_gender(x, y_male, y_female, title = 'BMI - Baseline', ylabel = 'Average', legendxy = "left")
  }
}
