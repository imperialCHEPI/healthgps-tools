library(randomForest)
library(tseries)
library(forecast)
require(dplyr)

country = "France"
myData = read.csv("InputData.csv")

########################################Random Forest Model Calibration########################################
##############################################################################################################


forest <- randomForest( HCEGDPRatio ~ ., data = myData, localImp = TRUE)
outputData = read.csv("France_HCE_summary_10KRuns.csv")
predicted_HCPGDPRatio = predict(forest, newdata = outputData)

########################################GDP Analysis using ARIMA Model########################################
##############################################################################################################

GDPData = read.csv("GDPPerCapita_1960_2021.csv")

gdp = GDPData$GDPPerCapita[GDPData$IHME.Country==country]
y = ts(gdp,start = c(1960,1),frequency = 1)

plot(y)

t = GDPData$Year[GDPData$IHME.Country==country]

summary(y)

plot(t,y)

#Dickey-Fuller Test for Stationarity
adf.test(y,alternative = "stationary",k=0)

#ACF and PACF
#acf(d.y)
#pacf(d.y)

ARIMAfit = auto.arima(y)
summary(ARIMAfit)

forecast = forecast(ARIMAfit, 30)
title = paste0("Predicting ", country, " GDP using ARIMA model")
plot(forecast, main = title, xlab="Year", ylab="LogGDP")

merged_ts <- ts(c(y, forecast$mean), start = start(y), frequency = frequency(1))

values = as.numeric(merged_ts)
years = as.numeric(time(merged_ts))

df = as.data.frame(values)
rownames(df) = years

n = length(outputData$time)
HCEDollars <-  rep(0, n)
GDPPerCapitaDollars <- rep(0, n) 

for(i in 1:n)
{
  year = toString(outputData$time[i])
  gdp_per_capita = df[year,]
  hce_gdp_ratio = predicted_HCPGDPRatio[i]
  hce_dollar = hce_gdp_ratio*gdp_per_capita
  HCEDollars[i] = hce_dollar
  GDPPerCapitaDollars[i] = gdp_per_capita
}

###############################################LOG Data####################################################
###########################################################################################################

#newDF <- cbind(cbind(cbind(outputData, predicted_HCPGDPRatio),GDPPerCapitaDollars), HCEDollars)

newDF <- cbind(outputData[c('source','run','time')], predicted_HCPGDPRatio, GDPPerCapitaDollars, HCEDollars)

write.csv(newDF, paste0(country,"_HCE_FinalOutput_small.csv"), row.names = FALSE)

newDF <- read.csv(paste0(country,"_HCE_FinalOutput_small.csv"))

result <- newDF %>% group_by(source,time) %>%
  summarise(count_run = n(), mean_predicted_HCPGDPRatio = mean(predicted_HCPGDPRatio),
                             mean_GDPPerCapitaDollars = mean(GDPPerCapitaDollars), 
                             mean_HCEDollars = mean(HCEDollars), .groups = "keep")

write.csv(result, paste0(country,"_HCE_FinalOutput_summary.csv"), row.names = FALSE)
