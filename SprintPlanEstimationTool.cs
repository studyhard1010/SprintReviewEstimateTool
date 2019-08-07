using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace EstimationTool
{
   
    class EstimationToolForSprintPlanning
    {
        private static int totalPreviousSprintDay = 15;
        private static int teamoffdayCurrentSprint;
        private static int teamOffDayPreviousSprint;
        private static int totalCurrentSprintDay = 15;
        private static int teamAverageVelocity;
        private static string IterationURL = "https://dev.azure.com/_apis/work/teamsettings/iterations?api-version=5.0";
        private static string previousSprintURL;
        private static string currentSprintURL;
        private static string previousSprintEndsDate;
        public static async Task GetProjects()
        {
            try
            {
                Console.WriteLine("Sign On");
                Console.Write("Username: ");
                var username = Console.ReadLine();
                Console.Write("Password: ");
                var password = Console.ReadLine();
                do
                {
                    Console.WriteLine("Please enter valid positive number, otherwise tryparse will fail!");
                    Console.Write("Team average velocity: ");
                    var input = Console.ReadLine();
                    int.TryParse(input, out teamAverageVelocity);
                } while (teamAverageVelocity == 0);

                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(
                    System.Text.ASCIIEncoding.ASCII.GetBytes(
                    string.Format("{0}:{1}", username, password))));
                    using (HttpResponseMessage responseIteration = await client.GetAsync(IterationURL))
                    {
                        int index = 0;
                        bool found = false;
                        responseIteration.EnsureSuccessStatusCode();
                        string responseBodyIteration = await responseIteration.Content.ReadAsStringAsync();
                        IterationsRootObject rbIteration = JsonConvert.DeserializeObject<IterationsRootObject>(responseBodyIteration);
                        foreach (IterationsValue valIteration in rbIteration.value)
                        {
                            if (valIteration.attributes.timeFrame == "current")
                            {
                                found = true;
                                Console.WriteLine($"Current sprint ends date: {valIteration.attributes.finishDate}");
                                break;
                            }

                            index++;
                        }

                        if (found == true)
                        {
                            currentSprintURL = $"{rbIteration.value[index].url}/capacities/?api-version=5.0";
                            if (index > 0)
                            {
                                previousSprintURL = $"{rbIteration.value[index - 1].url}/capacities/?api-version=5.0";
                                foreach (IterationsValue valIteration in rbIteration.value)
                                {
                                    if (valIteration.attributes.timeFrame == "past")
                                    {
                                        previousSprintEndsDate = $"{valIteration.attributes.finishDate}";
                                    }

                                }

                                Console.WriteLine($"Previous sprint ends date: {previousSprintEndsDate}");
                            }

                        }

                        else
                        {
                            Console.WriteLine("Error finding the sprint ends date");
                        }

                        using (HttpResponseMessage response = await client.GetAsync(previousSprintURL))
                        {
                            Console.Write("If previous sprint has team off day, please enter it. If no team off day, please put zero: ");
                            var inputPreviousSprint = Console.ReadLine();
                            int.TryParse(inputPreviousSprint, out teamOffDayPreviousSprint);
                            double totalPreviousCapacityOfGroups = 0.0;
                            response.EnsureSuccessStatusCode();
                            string responseBody = await response.Content.ReadAsStringAsync();
                            totalPreviousCapacityOfGroups += Helper(totalPreviousSprintDay, responseBody, teamOffDayPreviousSprint);
                            Console.WriteLine($"Total Capacity for the previous sprint: {totalPreviousCapacityOfGroups}");
                            using (HttpResponseMessage responseCurrent = await client.GetAsync(currentSprintURL))
                            {
                                Console.Write("If current sprint has team off day, please enter it. If no team off day, please put zero: ");
                                var inputCurrentSprint = Console.ReadLine();
                                int.TryParse(inputCurrentSprint, out teamoffdayCurrentSprint);
                                double totalCurrentCapacityOfGroups = 0.0;
                                responseCurrent.EnsureSuccessStatusCode();
                                string responseBodyCurrent = await responseCurrent.Content.ReadAsStringAsync();
                                totalCurrentCapacityOfGroups += Helper(totalCurrentSprintDay, responseBodyCurrent, teamoffdayCurrentSprint );
                                Console.WriteLine($"Total Capacity for the current sprint: {totalCurrentCapacityOfGroups}");
                                var estimatedEffortForCurrentSprint = totalCurrentCapacityOfGroups * teamAverageVelocity / totalPreviousCapacityOfGroups;
                                Console.WriteLine($"Estimated Effort for current sprint: {estimatedEffortForCurrentSprint:F0}");
                            }

                        }

                    }

                }

            }

            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing capacity per day: {ex.Message}");
            }

        }

        static async Task Main()
        {
            await GetProjects();
        }

        private static int Helper(int sprintDays, string responseBody, int diff)
        {
            int sum = 0;
            RootObject rb = JsonConvert.DeserializeObject<RootObject>(responseBody);
            foreach (Value val in rb.value)
            {
                double capacityPerDay = 0.0;
                double daysOffMemeber = 0.0;
                foreach (Activities act in val.activities)
                {
                    try
                    {
                        capacityPerDay = double.Parse(act.capacityPerDay);
                    }

                    catch (FormatException ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }

                }

                foreach (DaysOff daysoff in val.daysOff)
                {
                    try
                    {
                        DateTime start = Convert.ToDateTime(daysoff.start);
                        DateTime end = Convert.ToDateTime(daysoff.end);
                        TimeSpan ts = end.Subtract(start);
                        daysOffMemeber += ts.TotalDays + 1;
                    }

                    catch (FormatException ex)
                    {
                        Console.WriteLine($"Error calculating days off: {ex.Message}");
                    }

                }

                int totalCapacityOfMember = (int)((totalPreviousSprintDay - diff - daysOffMemeber) * capacityPerDay);
                sum += totalCapacityOfMember;
            }

            return sum;
        }

    }

    public class Activities
    {
        public string capacityPerDay { get; set; }
    }

    public class Attributes
    {
    }

    public class DaysOff
    {
        public string start { get; set; }
        public string end { get; set; }
    }

    public class Value
    {
        public List<Activities> activities { get; set; }
        public List<DaysOff> daysOff { get; set; }
        public List<Attributes> attributes { get; set; }
    }

    public class RootObject
    {
        public List<Value> value { get; set; }
    }

    public class IterationAttributes
    {
        public string finishDate { get; set; }
        public string timeFrame { get; set; }
    }

    public class IterationsValue
    {
        public IterationAttributes attributes { get; set; }
        public string url { get; set; }
    }

    public class IterationsRootObject
    {
        public List<IterationsValue> value { get; set; }
    }

}
