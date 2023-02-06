# ------------------------------------------------------------------------------
# Script to illustrate different ways to pivot and reshape HealthGPS results   |
# ------------------------------------------------------------------------------
#
# This script requires Tidyverse and Ggpubr packages for analysis and plotting,
# to install type (warning - both packages are big, many dependencies):
#
# install.packages("tidyverse")
# install.packages("ggpubr")
# ------------------------------------------------------------------------------

require(jsonlite)
require(dplyr)
require(ggplot2)
require(ggpubr)
require(stringr)
require(svglite)

get_latest_result_file <- function(output_folder){
  df <- file.info(Sys.glob(file.path(output_folder, "*.json")))
  last_result_filename = rownames(df)[which.max(df$mtime)]
  return(last_result_filename)
}

# Finds the latest result file (json format)
country_name = 'United Kingdom'

#output_folder = 'C:/HealthGPS/Results';
output_folder = paste0('C:/HealthGPS/Results/', country_name, '/Summary')
#output_folder = paste0('C:/Workspace/Results/', country_name);
#output_folder = paste0('C:/HealthGPS/Results/', country_name);

last_result_filename = get_latest_result_file(output_folder)
stopifnot(nchar(last_result_filename) >= 2)

#-------------------------------------------------------------------------------
# Load json data into r struct, if not using a live output folder, just replace 
# 'last_result_filename' with the full name of the output file to load, e.g.
#
# last_result_filename <- "C:/xxx/yyy.json"

print(paste("Loading results file: ", last_result_filename))
data <- fromJSON(last_result_filename)

sim_groups <- data.frame(data$result$source, data$result$run, data$result$time)
colnames(sim_groups) <- c("scenario", "run", "time")

# Clear existing plots
graphics.off()
gc()

# Default configuration
default_theme <- theme_light()
default_gender_colour <- scale_color_manual(values=c("male"="blue","female"="red"))
default_scenario_colour <- scale_color_manual(values=c("baseline"="blue","intervention"="red"))
default_scenario_linestyle <- scale_linetype_manual(values=c("baseline"="solid","intervention"="twodash"))
default_x_scale <- scale_x_continuous(breaks = scales::pretty_breaks(n = 10))
start_time <- min(data$result$time)
finish_time <- max(data$result$time)

ci_level <- 1 - (0.05/2)
line_size = 0.5
img_dpi = 300 #"retina" (320), "print" (300), or "screen" (72)
img_units = "cm"
img_width = 25;
img_height = 15;

plot_output_path <- paste0(output_folder, '/Plots/', country_name, '_')
dir.create(file.path(paste0(output_folder, '/Plots/')), showWarnings = FALSE)

# Population
if (T) {
  sim_data <- cbind(sim_groups, data$result$population)
  
  info <- sim_data %>% group_by(scenario, time) %>% filter(time > start_time) %>%
    summarise(count_run = n(),
              avg_size = mean(size, na.rm = TRUE),   sd_size = sd(size, na.rm = TRUE),
              avg_alive = mean(alive, na.rm = TRUE), sd_alive = sd(alive, na.rm = TRUE),
              avg_migrating = mean(migrating, na.rm = TRUE), sd_migrating = sd(migrating, na.rm = TRUE),
              avg_dead = mean(dead, na.rm = TRUE), sd_dead = sd(dead, na.rm = TRUE),
              .groups = "keep")
  
  p1 <- ggplot(data=info, aes(x=time, y=avg_size, group=scenario)) + 
    geom_line(aes(color=scenario)) + default_theme + default_scenario_colour + default_x_scale +
    scale_y_continuous(breaks = pretty(info$avg_size, n = 8)) +
    ggtitle("Population Allocated") + xlab("Year") + ylab("Average")
  
  p2 <- ggplot(data=info, aes(x=time, y=avg_alive, group=scenario)) +
    geom_line(aes(color=scenario)) + default_theme + default_scenario_colour + default_x_scale +
    scale_y_continuous(breaks = pretty(info$avg_alive, n = 8)) +
    ggtitle("Population Alive") + xlab("Year") + ylab("Average")
  
  p3 <- ggplot(data=info, aes(x=time, y=avg_migrating, group=scenario)) +
    geom_line(aes(color=scenario)) + default_theme + default_scenario_colour + default_x_scale +
    scale_y_continuous(breaks = pretty(info$avg_migrating, n = 8)) +
    ggtitle("Population Migrating") + xlab("Year") + ylab("Average")

  p4 <- ggplot(data=info, aes(x=time, y=avg_dead, group=scenario)) +
    geom_line(aes(color=scenario)) + default_theme + default_scenario_colour + default_x_scale +
    scale_y_continuous(breaks = pretty(info$avg_dead, n = 8)) +
    ggtitle("Population Dead") + xlab("Year") + ylab("Average")
  
  p = ggarrange(p1, p2, p3, p4, ncol = 2, nrow = 2)
  show(p)
  
  szs = 1.2
  ggsave(paste0(plot_output_path, "Population.png"), device = "png",
         units=img_units, width=szs*img_width, height=szs*img_height, dpi=img_dpi)
  
  ggsave(paste0(plot_output_path, "Population.svg"), device = "svg",
         units=img_units, width=szs*img_width, height=szs*img_height, dpi=img_dpi)
  
  write.csv(info, paste0(plot_output_path, "Population.csv"), row.names = FALSE)
}

# Indicators
if (T) {
  sim_data <- cbind(sim_groups, data$result$indicators)
  
  info <- sim_data %>% group_by(scenario, time) %>% filter(time > start_time) %>%
    summarise(count_run = n(), avg_yll = mean(YLL), avg_yld = mean(YLD),
              avg_daly= mean(DALY), .groups = "keep")

  p1 <- ggplot(data=info, aes(x=time, y=avg_yll, group=scenario)) + 
    geom_line(aes(color=scenario)) + default_theme + default_scenario_colour + default_x_scale +
    scale_y_continuous(breaks = pretty(info$avg_yll, n = 8)) +
    ggtitle("Years of Life Lost - YLL") + xlab("Year") + ylab("Average")
  
  p2 <- ggplot(data=info, aes(x=time, y=avg_yld, group=scenario)) +
    geom_line(aes(color=scenario)) + default_theme + default_scenario_colour + default_x_scale +
    scale_y_continuous(breaks = pretty(info$avg_yld, n = 6)) +
    ggtitle("Years Lost due to Disability - YLD") + xlab("Year") + ylab("Average")
  
  p3 <- ggplot(data=info, aes(x=time, y=avg_daly, group=scenario)) +
    geom_line(aes(color=scenario))  + default_theme + default_scenario_colour + default_x_scale +
    scale_y_continuous(breaks = pretty(info$avg_daly, n = 6)) +
    ggtitle("Disability-Adjusted Life Years - DALY") + xlab("Year") + ylab("Average")
  
  p <- ggarrange(p1, p2, p3, ncol = 1, nrow = 3)
  show(p);
  
  szs = 1.2
  ggsave(paste0(plot_output_path, "Indicators.png"),  device = "png",
         units=img_units, width=szs*img_width, height=szs*img_height, dpi=img_dpi)
  
  ggsave(paste0(plot_output_path, "Indicators.svg"),  device = "svg",
         units=img_units, width=szs*img_width, height=szs*img_height, dpi=img_dpi)
  
  write.csv(info, paste0(plot_output_path, "Indicators_Table.csv"), row.names = FALSE)
}

# Metrics
if (T){
  sim_data <- cbind(sim_groups, data$result$metrics)
  rm(info)
  info <- sim_data %>% group_by(scenario, time) %>% filter(time > start_time) %>%
    summarise(count_run = n(), avg_exp_death = mean(ExpectedDeathRate),
              avg_sim_death = mean(SimulatedDeathRate), 
              avg_diff_death= mean(DeathRateDeltaPercent), .groups = "keep")
  
  p1 <- ggplot(data=info, aes(x=time, y=avg_exp_death, group=scenario)) + 
    geom_line(aes(color=scenario)) + default_theme + default_scenario_colour + default_x_scale +
    scale_y_continuous(breaks = pretty(info$avg_exp_death, n = 6)) +
    ggtitle("Expected Death Rate") + xlab("Year") + ylab("x1000")
  
  p2 <- ggplot(data=info, aes(x=time, y=avg_sim_death, group=scenario)) +
    geom_line(aes(color=scenario)) + default_theme + default_scenario_colour + default_x_scale +
    scale_y_continuous(breaks = pretty(info$avg_sim_death, n = 6)) +
    ggtitle("Simulated Death Rate") + xlab("Year") + ylab("x1000")
  
  p3 <- ggplot(data=info, aes(x=time, y=avg_diff_death, group=scenario)) +
    geom_line(aes(color=scenario)) + default_theme + default_scenario_colour + default_x_scale +
    scale_y_continuous(breaks = pretty(info$avg_diff_death, n = 6)) +
    ggtitle("Death Rate Delta") + xlab("Year") + ylab("Percent")
  
  p <- ggarrange(p1, p2, p3, ncol = 1, nrow = 3)
  show(p);
  
  ggsave(paste0(plot_output_path, "Metrics.png"), device = "png",
         units=img_units, width=img_width, height=img_height, dpi=img_dpi)
  
  ggsave(paste0(plot_output_path, "Metrics.svg"), device = "svg",
         units=img_units, width=img_width, height=img_height, dpi=img_dpi)
  
  write.csv(info, paste0(plot_output_path, "Metrics.csv"), row.names = FALSE)
}

# Average age
if (T) {
  sim_data <- cbind(sim_groups, data$result$average_age)
  # pivot data
  info <- sim_data %>% group_by(scenario, time) %>% 
    summarise(runs = n(),
              avg_male = mean(male, na.rm = TRUE),
              sd_male = sd(male, na.rm = TRUE),
              avg_female = mean(female, na.rm = TRUE),
              sd_female = sd(female, na.rm = TRUE),
              .groups = "keep")
  
  # reshape
  df <- data.frame(scenario = info$scenario, time = info$time, runs = info$runs,
                   bmi = c(info$avg_male, info$avg_female),
                   sd = c(info$sd_male, info$sd_female),
                   se = c(info$sd_male / sqrt(info$runs), info$sd_female) / sqrt(info$runs),
                   gender = c(rep('male', nrow(info)), rep('female', nrow(info))))
  
  df$ci <- qt(ci_level, df$runs - 1) * df$se
  
  p <- ggplot(data=df, aes(x=time, y=bmi, group=interaction(scenario, gender))) +
    geom_line(size=0.6, aes(linetype=scenario, color=gender)) +
    #geom_ribbon(aes(ymin=bmi-ci, ymax=bmi+ci, fill=gender), alpha=0.1, show.legend=FALSE) +
    default_theme + default_scenario_linestyle + default_gender_colour +
    scale_x_continuous(breaks = pretty(df$time, n = 10)) +
    scale_y_continuous(breaks = pretty(df$bmi, n = 10)) +
    ggtitle("Population average age under two scenarios") +
    xlab("Year") + ylab("Average")
  
  show(p)
  
  ggsave(paste0(plot_output_path, "PopulationAge.png"),  device = "png",
        units=img_units, width=img_width, height=img_height, dpi=img_dpi)
  
  ggsave(paste0(plot_output_path, "PopulationAge.svg"),  device = "svg",
         units=img_units, width=img_width, height=img_height, dpi=img_dpi)
  
  write.csv(df, paste0(plot_output_path, "PopulationAge.csv"), row.names = FALSE)
}

# Diseases
if (T) {
  disease_plots = list()
  for (disease in colnames(data$result$disease_prevalence)) {
    sim_data <- cbind(sim_groups, data$result$disease_prevalence[[disease]])

    info <- sim_data %>% group_by(scenario, time) %>% 
      summarise(count_run = n(), avg_male = mean(male),
                avg_female = mean(female), .groups = "keep")
    
    # reshape
    df <- data.frame(scenario = info$scenario, time = info$time,
                   prevalence = c(info$avg_male, info$avg_female),
                   gender = c(rep('male', nrow(info)), rep('female', nrow(info))))
    
    disease_plots[[disease]] <- ggplot(df, aes(x=time, y=prevalence, group=interaction(scenario, gender))) +
      geom_line(size=0.6, aes(linetype=scenario, color=gender)) + default_theme +
      default_scenario_linestyle + default_gender_colour +
      scale_x_continuous(breaks = pretty(df$time, n = 10)) +
      scale_y_continuous(breaks = pretty(df$prevalence, n = 8)) +
      
      ggtitle(paste(str_to_title(disease), " projection")) + xlab("Year") + ylab("Prevalence (%)")
  }
  
  p <- ggarrange(plotlist=disease_plots, ncol = 2, nrow = ceiling(length(disease_plots)/2))
  show(p);
  
  szs = 1.5
  ggsave(paste0(plot_output_path, "Diseases.png"), device = "png",
         units=img_units, width=szs*img_width, height=szs*img_height, dpi=img_dpi)
  
  ggsave(paste0(plot_output_path, "Diseases.svg"), device = "svg",
         units=img_units, width=szs*img_width, height=szs*img_height, dpi=img_dpi)
}

# Single disease
if (T) {
  for (disease in colnames(data$result$disease_prevalence)) {
    #disease <- "diabetes"
    sim_data <- cbind(sim_groups, data$result$disease_prevalence[[disease]])
      
    info <- sim_data %>% group_by(scenario, time) %>% 
        summarise(count_run = n(), avg_male = mean(male), avg_female = mean(female), .groups = "keep")
      
    # reshape
    df <- data.frame(scenario = info$scenario, time = info$time,
                     prevalence = c(info$avg_male, info$avg_female),
                     gender = c(rep('male', nrow(info)), rep('female', nrow(info))))
      
    p <- ggplot(df, aes(x=time, y=prevalence, group=interaction(scenario, gender))) +
        geom_line(size=0.6, aes(linetype=scenario, color=gender)) + default_theme +
        default_scenario_linestyle + default_gender_colour +
        scale_x_continuous(breaks = pretty(df$time, n = 10)) +
        scale_y_continuous(breaks = pretty(df$prevalence, n = 10)) +
        ggtitle(paste(str_to_title(disease), "projection under two scenarios")) +
        xlab("Year") + ylab("Prevalence (%)")
    
    show(p);
    
    ggsave(paste0(plot_output_path,"Disease_", str_to_title(disease), ".png"), device = "png",
           units=img_units, width=img_width, height=img_height, dpi=img_dpi)
    
    ggsave(paste0(plot_output_path,"Disease_", str_to_title(disease), ".svg"), device = "svg",
           units=img_units, width=img_width, height=img_height, dpi=img_dpi)
    
    write.csv(df, paste0(plot_output_path,"Disease_", str_to_title(disease), ".csv"), row.names = FALSE)
  }
}

# Risk Factors
if (T) {
  risk_plots = list()
  for (risk_factor in colnames(data$result$risk_factors_average)) {
    sim_data <- cbind(sim_groups, data$result$risk_factors_average[[risk_factor]])
    
    info <- sim_data %>% group_by(scenario, time) %>% 
      summarise(count_run = n(), avg_male = mean(male), avg_female = mean(female), .groups = "keep")
    
    # reshape
    df <- data.frame(scenario = info$scenario, time = info$time,
                     value = c(info$avg_male, info$avg_female),
                     gender = c(rep('male', nrow(info)), rep('female', nrow(info))))
    
    risk_plots[[risk_factor]] <- ggplot(df, aes(x=time, y=value, group=interaction(scenario, gender))) +
      geom_line(size=line_size, aes(linetype=scenario, color=gender)) + default_theme +
      default_scenario_linestyle + default_gender_colour +
      scale_x_continuous(breaks = pretty(df$time, n = 10)) +
      scale_y_continuous(breaks = pretty(df$value, n = 8)) +
      ggtitle(risk_factor) + xlab("Year") + ylab("Average")
  }
  
  p <- ggarrange(plotlist=risk_plots, ncol = 2, nrow = ceiling(length(risk_plots)/2))
  show(p);
  
  szs = 1.2;
  ggsave(paste0(plot_output_path,"RiskFactors.png"), device = "png",
         units=img_units, width=szs*img_width, height=szs*img_height, dpi=img_dpi)
  
  ggsave(paste0(plot_output_path,"RiskFactors.svg"), device = "svg",
         units=img_units, width=szs*img_width, height=szs*img_height, dpi=img_dpi)  
}

# Single Risk Factor
if (T) {
  for (risk_factor in colnames(data$result$risk_factors_average)) {
    #risk_factor <- "BMI"
    sim_data <- cbind(sim_groups, data$result$risk_factors_average[[risk_factor]])
    # pivot data
    info <- sim_data %>% group_by(scenario, time) %>% 
      summarise(runs = n(),
                avg_male = mean(male, na.rm = TRUE),
                sd_male = sd(male, na.rm = TRUE),
                avg_female = mean(female, na.rm = TRUE),
                sd_female = sd(female, na.rm = TRUE),
                .groups = "keep")
    
      # reshape
    df <- data.frame(scenario = info$scenario, time = info$time, runs = info$runs,
                     avg = c(info$avg_male, info$avg_female),
                     sd = c(info$sd_male, info$sd_female),
                     se = c(info$sd_male / sqrt(info$runs), info$sd_female) / sqrt(info$runs),
                     gender = c(rep('male', nrow(info)), rep('female', nrow(info))))
    
    df$ci <- qt(ci_level, df$runs - 1) * df$se
    
    p <- ggplot(data=df, aes(x=time, y=avg, group=interaction(scenario, gender))) +
         geom_line(size=line_size, aes(linetype=scenario, color=gender)) +
         #geom_ribbon(aes(ymin=bmi-ci, ymax=bmi+ci, fill=gender), alpha=0.1, show.legend=FALSE) +
         default_theme + default_scenario_linestyle + default_gender_colour +
         scale_x_continuous(breaks = pretty(df$time, n = 10)) +
         scale_y_continuous(breaks = pretty(df$avg, n = 10)) +
         ggtitle(paste(risk_factor, " projection under two scenarios")) +
         xlab("Year") + ylab("Average")
    
    show(p)
    
    ggsave(paste0(plot_output_path, "RiskFactor_", risk_factor, ".png"), device = "png",
           units=img_units, width=img_width, height=img_height, dpi=img_dpi)
    
    ggsave(paste0(plot_output_path, "RiskFactor_", risk_factor, ".svg"), device = "svg",
           units=img_units, width=img_width, height=img_height, dpi=img_dpi)
    
    write.csv(df, paste0(plot_output_path, "RiskFactor_", risk_factor, ".csv"), row.names = FALSE)
  }
}

# co-morbidity
if (T) {
  morbidity_plots = list()
  disease_free = first(colnames(data$result$comorbidities))
  last_plus = last(colnames(data$result$comorbidities))
  final_table = data.frame(matrix(ncol = 5, nrow = 0))
  colnames(final_table) <- c('scenario', 'time','avg_value','gender','ndiseases')
  full_table <- final_table
  
  for (morbidity in colnames(data$result$comorbidities)) {
    sim_data <- cbind(sim_groups, data$result$comorbidities[[morbidity]])
    
    info <- sim_data %>% group_by(scenario, time) %>% 
      summarise(count_run = n(), avg_male = mean(male),
                avg_female = mean(female), .groups = "keep")
    
    # reshape
    df <- data.frame(scenario = info$scenario, time = info$time,
                     avg_value = c(info$avg_male, info$avg_female),
                     gender = c(rep('male', nrow(info)), rep('female', nrow(info))),
                     ndiseases = c(rep(strtoi(morbidity), nrow(info))))
    
    final_table = rbind(final_table, df %>% filter(time == finish_time))
    full_table =  rbind(full_table, df)
    
    morbidity_title = paste("Comorbidity # ", morbidity, " projection", sep="")
    if (morbidity == disease_free){
      morbidity_title = paste("Comorbidity # ", morbidity, " (disease free) projection", sep="")
    }
    
    if (morbidity == last_plus){
      morbidity_title = paste("Comorbidity # ", morbidity, "+ projection", sep="")
    }
    
    morbidity_plots[[morbidity]] <- ggplot(df, aes(x=time, y=avg_value, group=interaction(scenario, gender))) +
      geom_line(size=0.6, aes(linetype=scenario, color=gender)) + default_theme +
      default_scenario_linestyle + default_gender_colour +
      scale_x_continuous(breaks = pretty(df$time, n = 10)) +
      scale_y_continuous(breaks = pretty(df$avg_value, n = 8)) +
      
      ggtitle(morbidity_title) + xlab("Year") + ylab("Prevalence (%)")
  }
  
  p <- ggarrange(plotlist=morbidity_plots, ncol = 2, nrow = ceiling(length(morbidity_plots)/2))
  show(p);
  
  szs = 1.5
  ggsave(paste0(plot_output_path, "Comorbidities.png"), device = "png",
         units=img_units, width=szs*img_width, height=szs*img_height, dpi=img_dpi)
  
  ggsave(paste0(plot_output_path, "Comorbidities.svg"), device = "svg",
         units=img_units, width=szs*img_width, height=szs*img_height, dpi=img_dpi)
  
  # Plot comorbidities table
  p <- ggplot(final_table, aes(fill=interaction(scenario, gender), y=avg_value, x=ndiseases)) +
       geom_bar(position = position_dodge(width = 0.9), stat="identity", width=.8, colour="darkgray") +
       default_theme + scale_fill_manual(values=c("bisque", "coral", "cyan", "deepskyblue")) +
       scale_x_continuous(breaks = pretty(final_table$ndiseases)) +
       scale_y_continuous(breaks = pretty(final_table$avg_value, n = 10)) +
       ggtitle(paste("Comorbidities projection to",finish_time)) + xlab("# of diseases") + ylab("Prevalence (%)")
  show(p);
  
  ggsave(paste0(plot_output_path, "Comorbidities_Table.png"), device = "png",
         units=img_units, width=szs*img_width, height=szs*img_height, dpi=img_dpi)
  
  ggsave(paste0(plot_output_path, "Comorbidities_Table.svg"), device = "svg",
         units=img_units, width=szs*img_width, height=szs*img_height, dpi=img_dpi)
  
  write.csv(full_table, paste0(plot_output_path, "Comorbidities_Table.csv"), row.names = FALSE)
}
