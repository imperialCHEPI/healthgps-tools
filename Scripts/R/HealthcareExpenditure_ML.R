library(randomForest)

myData = read.csv("HealthCareExpenditures_AllDiseases.csv")

forest <- randomForest( HealthCareExpenditurePerCapita~ ., data = myData, localImp = TRUE)


outputData = read.csv("Summary_Ali.csv")

predicted_HCP = predict(forest, newdata = outputData)

#newDF <- cbind(outputData, predicted_HCP)
newDF <- cbind(outputData[c('source','run','time')], predicted_HCP)

write.csv(newDF,"FinalOutPut.csv",row.names = FALSE)