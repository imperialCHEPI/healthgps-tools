require(dplyr)
require(ggplot2)

# create groups frame
groups <- data.frame(data$result$source, data$result$run, data$result$time)
colnames(groups) <- c("scenario", "run", "time")

# create dataset
risk_factor <- "BMI"
sim_data <- cbind(groups, data$result$risk_factors_average[[risk_factor]])

# pivot dataset
info <- sim_data %>% group_by(scenario, time) %>% 
  summarise(runs = n(),
            avg_male = mean(male, na.rm = TRUE),
            sd_male = sd(male, na.rm = TRUE),
            avg_female = mean(female, na.rm = TRUE),
            sd_female = sd(female, na.rm = TRUE),
            .groups = "keep")

# reshape dataset
df <- data.frame(scenario = info$scenario, time = info$time, runs = info$runs,
                 bmi = c(info$avg_male, info$avg_female),
                 sd = c(info$sd_male, info$sd_female),
                 se = c(info$sd_male / sqrt(info$runs), info$sd_female) / sqrt(info$runs),
                 gender = c(rep('male', nrow(info)), rep('female', nrow(info))))

p <- ggplot(data=df, aes(x=time, y=bmi, group=interaction(scenario, gender))) +
  geom_line(size=0.6, aes(linetype=scenario, color=gender)) + theme_light() +
  scale_linetype_manual(values=c("baseline"="solid","intervention"="longdash")) +
  scale_color_manual(values=c("male"="blue","female"="red")) +
  scale_x_continuous(breaks = pretty(df$time, n = 10)) +
  scale_y_continuous(breaks = pretty(df$bmi, n = 10)) +
  ggtitle(paste(risk_factor, " projection under two scenarios")) +
  xlab("Year") + ylab("Average")

show(p)
ggsave("C:/Source/healthgps/assets/image/bmi_projection.png", device = "png")
ggsave("C:/Source/healthgps/assets/image/bmi_projection.svg", device = "svg")

