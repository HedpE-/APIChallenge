using Newtonsoft.Json;
using NUnit.Framework;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace APIChallenge
{
    [TestFixture]
    public class APIChallengeTests
    {
        #region Private members
        private readonly string ApiServerBinaryFileName = "api_challenge-3.156.0_windows.exe";
        private readonly string ApiServerBinaryPath = "API_Server";
        private readonly string AssemblyName;
        private string OutputServerFilePath;
        private string UsersDbFilePath;
        private string EmployeesDbFilePath;
        private readonly IRestClient Client = new RestClient("http://localhost:5000/api");
        #endregion Private members

        #region Constructor
        public APIChallengeTests()
        {
            AssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            ApiServerBinaryPath = AssemblyName + "." + ApiServerBinaryPath + "." + ApiServerBinaryFileName;
        }
        #endregion Constructor

        #region Test Cases
        [Test]
        public void TC01_FirstRequirement()
        {
            // Register a new user
            IRestRequest request = new RestRequest("/register");
            request.AddParameter("email", "rui@email.com");
            request.AddParameter("password", "123456");
            request.AddParameter("role", "admin");
            IRestResponse response = Client.Post(request);

            Assert.IsTrue(response.IsSuccessful);

            var json = DeserializeJson<IDictionary<string, object>>(response.Content);

            // Check if the user was successfully created(not only by call response)
            Assert.IsTrue(json.ContainsKey("success") && ((bool)json["success"]) && json.ContainsKey("message") && json["message"].ToString() == "created");

            response = Client.Post(request);

            Assert.IsFalse(response.IsSuccessful);

            json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("message") && json["message"].ToString() == "user already registered");
        }

        [Test]
        public void TC02_SecondRequirement()
        {
            // Perform login existent user
            Client.Authenticator = new HttpBasicAuthenticator("rui@email.com", "123456");
            IRestRequest request = new RestRequest("/login");
            IRestResponse response = Client.Post(request);

            // Check if login was successfully performed
            Assert.IsTrue(response.IsSuccessful);

            var json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("success") && ((bool)json["success"]) && json.ContainsKey("token"));

            var token = json["token"].ToString();

            Assert.IsFalse(string.IsNullOrEmpty(token));

            // Try to perform login using a nonexistent user
            Client.Authenticator = new HttpBasicAuthenticator("nonExistentUser@email.com", "pass");
            request = new RestRequest("/login");
            response = Client.Post(request);

            Assert.IsTrue(response.IsSuccessful);

            json = DeserializeJson<IDictionary<string, object>>(response.Content);

            // Check if non existent user can access by checking if a token was returned, if token is empty, user doesn't have access

            Assert.IsTrue(json.ContainsKey("success") && ((bool)json["success"]) && json.ContainsKey("token"));

            token = json["token"].ToString();

            Assert.IsTrue(string.IsNullOrEmpty(token));
        }

        [Test]
        public void TC03_ThirdRequirement()
        {
            // Perform login existent user
            Client.Authenticator = new HttpBasicAuthenticator("rui@email.com", "123456");
            IRestRequest request = new RestRequest("/login");
            IRestResponse response = Client.Post(request);

            // Check if login was successfully performed
            Assert.IsTrue(response.IsSuccessful);

            var json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("success") && ((bool)json["success"]) && json.ContainsKey("token"));

            var token = json["token"].ToString();

            Assert.IsFalse(string.IsNullOrEmpty(token));

            request = new RestRequest("/employees/all");
            request.AddHeader("accessToken", token);
            response = Client.Get(request);

            Assert.IsTrue(response.IsSuccessful);

            var employees = DeserializeJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>(response.Content);

            Assert.IsTrue(employees.ContainsKey("employees"));

            var initialEmployeesCount = employees["employees"].Count();

            // Create one or more new employees
            request = new RestRequest("/employees");
            request.AddHeader("accessToken", token);
            request.AddParameter("firstname", "Ronnie");
            request.AddParameter("lastname", "Radke");
            request.AddParameter("email", "rradke@email.com");
            response = Client.Post(request);

            Assert.IsTrue(response.IsSuccessful);

            json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("success") && ((bool)json["success"]) && json.ContainsKey("message") && json["message"].ToString().StartsWith("id="));

            request = new RestRequest("/employees");
            request.AddHeader("accessToken", token);
            request.AddParameter("firstname", "Randy");
            request.AddParameter("lastname", "Bluther");
            request.AddParameter("email", "blythe@email.com");
            response = Client.Post(request);

            Assert.IsTrue(response.IsSuccessful);

            json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("success") && ((bool)json["success"]) && json.ContainsKey("message") && json["message"].ToString().StartsWith("id="));

            var employeeIdToEdit = int.Parse(json["message"].ToString().Replace("id=", ""));

            // Check if the employees were successfully created
            request = new RestRequest("/employees/all");
            request.AddHeader("accessToken", token);
            response = Client.Get(request);

            Assert.IsTrue(response.IsSuccessful);

            employees = DeserializeJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>(response.Content);

            Assert.IsTrue(employees.ContainsKey("employees") && employees["employees"].Count() == initialEmployeesCount + 2);

            // Update one or more employees
            request = new RestRequest("/employees");
            request.AddHeader("accessToken", token);
            request.AddParameter("firstname", "Randy");
            request.AddParameter("lastname", "Blythe");
            request.AddParameter("email", "blythe@email.com");
            request.AddParameter("id", employeeIdToEdit);
            response = Client.Put(request);

            Assert.IsTrue(response.IsSuccessful);

            json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("success") && ((bool)json["success"]) && json.ContainsKey("message") && json["message"].ToString() == "updated");

            // Check if the employees were successfully updated
            request = new RestRequest("/employees/" + employeeIdToEdit);
            request.AddHeader("accessToken", token);
            response = Client.Get(request);

            Assert.IsTrue(response.IsSuccessful);

            json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("last_name") && json["last_name"].ToString() == "Blythe");
        }

        [Test]
        public void TC04_FourthRequirement()
        {
            // Perform login existent user
            Client.Authenticator = new HttpBasicAuthenticator("rui@email.com", "123456");
            IRestRequest request = new RestRequest("/login");
            IRestResponse response = Client.Post(request);

            // Check if login was successfully performed
            Assert.IsTrue(response.IsSuccessful);

            var json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("success") && ((bool)json["success"]) && json.ContainsKey("token"));

            var token = json["token"].ToString();

            Assert.IsFalse(string.IsNullOrEmpty(token));

            request = new RestRequest("/employees/all");
            request.AddHeader("accessToken", token);
            response = Client.Get(request);

            Assert.IsTrue(response.IsSuccessful);

            var employees = DeserializeJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>(response.Content);

            Assert.IsTrue(employees.ContainsKey("employees"));

            var initialEmployeesCount = employees["employees"].Count();

            var employeeIdToDelete = int.Parse(employees["employees"].First()["id"].ToString());

            // Delete one or more employees
            request = new RestRequest("/employees");
            request.AddHeader("accessToken", token);
            request.AddJsonBody("{\"ids\":" + employeeIdToDelete + "}");
            response = Client.Delete(request);

            Assert.IsTrue(response.IsSuccessful);

            json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("success") && ((bool)json["success"]) && json.ContainsKey("message") && json["message"].ToString() == "deleted");

            // Check if the employees were successfully deleted
            request = new RestRequest("/employees/all");
            request.AddHeader("accessToken", token);
            response = Client.Get(request);

            Assert.IsTrue(response.IsSuccessful);

            employees = DeserializeJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>(response.Content);

            Assert.IsTrue(employees.ContainsKey("employees") && employees["employees"].Count() == initialEmployeesCount - 1);

            request = new RestRequest("/employees/" + employeeIdToDelete);
            request.AddHeader("accessToken", token);
            response = Client.Get(request);

            Assert.IsFalse(response.IsSuccessful);

            json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("success") && !((bool)json["success"]) && json.ContainsKey("message") && json["message"].ToString() == "employee not found");
        }

        [Test]
        public void TC05_FifthRequirement_GetEmployee_AlternateMethod()
        {
            // Perform login existent user
            Client.Authenticator = new HttpBasicAuthenticator("rui@email.com", "123456");
            IRestRequest request = new RestRequest("/login");
            IRestResponse response = Client.Post(request);

            // Check if login was successfully performed
            Assert.IsTrue(response.IsSuccessful);

            var json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("success") && ((bool)json["success"]) && json.ContainsKey("token"));

            var token = json["token"].ToString();

            Assert.IsFalse(string.IsNullOrEmpty(token));

            request = new RestRequest("/employees/all");
            request.AddHeader("accessToken", token);
            response = Client.Get(request);

            Assert.IsTrue(response.IsSuccessful);

            var employees = DeserializeJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>(response.Content);

            Assert.IsTrue(employees.ContainsKey("employees"));

            var employeeId = int.Parse(employees["employees"].First()["id"].ToString());

            request = new RestRequest("/employees");
            request.AddHeader("accessToken", token);
            request.AddParameter("id", employeeId);
            response = Client.Get(request);

            Assert.IsTrue(response.IsSuccessful);

            json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.Keys.Count == 4 && !string.IsNullOrEmpty(json["first_name"].ToString()) && !string.IsNullOrEmpty(json["last_name"].ToString()) && !string.IsNullOrEmpty(json["email"].ToString()) && !string.IsNullOrEmpty(json["id"].ToString()));
        }

        [Test]
        public void TC06_FifthRequirement_UpdateEmployee_PatchMethod()
        {
            // Perform login existent user
            Client.Authenticator = new HttpBasicAuthenticator("rui@email.com", "123456");
            IRestRequest request = new RestRequest("/login");
            IRestResponse response = Client.Post(request);

            // Check if login was successfully performed
            Assert.IsTrue(response.IsSuccessful);

            var json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("success") && ((bool)json["success"]) && json.ContainsKey("token"));

            var token = json["token"].ToString();

            Assert.IsFalse(string.IsNullOrEmpty(token));

            request = new RestRequest("/employees/all");
            request.AddHeader("accessToken", token);
            response = Client.Get(request);

            Assert.IsTrue(response.IsSuccessful);

            var employees = DeserializeJson<IDictionary<string, IEnumerable<IDictionary<string, object>>>>(response.Content);

            Assert.IsTrue(employees.ContainsKey("employees"));

            var employeeId = int.Parse(employees["employees"].First()["id"].ToString());
            var employeeFirstName = employees["employees"].First(e => int.Parse(e["id"].ToString()) == employeeId)["first_name"].ToString();
            var employeeLastName = employees["employees"].First(e => int.Parse(e["id"].ToString()) == employeeId)["last_name"].ToString();
            var employeeNewEmail = (employeeFirstName + "." + employeeLastName).Replace("ç", "c").ToLower() + "@email.com";

            Assert.IsFalse(employees["employees"].First(e => int.Parse(e["id"].ToString()) == employeeId)["email"].ToString() == employeeNewEmail);

            // Update employee data using PATCH method
            request = new RestRequest("/employees");
            request.AddHeader("accessToken", token);
            request.AddParameter("firstname", employeeFirstName);
            request.AddParameter("lastname", employeeLastName);
            request.AddParameter("email", employeeNewEmail);
            request.AddParameter("id", employeeId);
            response = Client.Patch(request);

            Assert.IsTrue(response.IsSuccessful);

            json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("success") && ((bool)json["success"]) && json.ContainsKey("message") && json["message"].ToString() == "updated");

            // Check if the employees were successfully updated
            request = new RestRequest("/employees/" + employeeId);
            request.AddHeader("accessToken", token);
            response = Client.Get(request);

            Assert.IsTrue(response.IsSuccessful);

            json = DeserializeJson<IDictionary<string, object>>(response.Content);

            Assert.IsTrue(json.ContainsKey("email") && json["email"].ToString() == employeeNewEmail);
        }
        #endregion Test Cases

        #region Setup & TearDown methods
        [SetUp]
        public void Setup()
        {
            var tempFilesPath = Path.GetTempPath();
            OutputServerFilePath = Path.Combine(tempFilesPath, ApiServerBinaryFileName);
            UsersDbFilePath = Path.Combine(tempFilesPath, "authdb");
            EmployeesDbFilePath = Path.Combine(tempFilesPath, "employeesdb");

            var processes = Process.GetProcessesByName(ApiServerBinaryFileName);
            if (processes.Any())
            {
                foreach(var process in processes)
                    process.Kill();
            }

            if (File.Exists(OutputServerFilePath))
                File.Delete(OutputServerFilePath);

            if (File.Exists(UsersDbFilePath))
                File.Delete(UsersDbFilePath);

            if (File.Exists(EmployeesDbFilePath))
                File.Delete(EmployeesDbFilePath);

            ExtractResourceToLocalFile(ApiServerBinaryPath, AssemblyName, OutputServerFilePath);

            Assert.IsTrue(File.Exists(OutputServerFilePath), "Faile to execute the API Server.");

            Process.Start(OutputServerFilePath);

            Thread.Sleep(5000);
        }

        [TearDown]
        public void TearDown()
        {
            var processes = Process.GetProcessesByName(ApiServerBinaryFileName.Replace(".exe", ""));
            if (processes.Any())
            {
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit();
                    }
                    catch { }
                }
            }

            if (File.Exists(OutputServerFilePath))
                File.Delete(OutputServerFilePath);

            if (File.Exists(UsersDbFilePath))
                File.Delete(UsersDbFilePath);

            if (File.Exists(EmployeesDbFilePath))
                File.Delete(EmployeesDbFilePath);
        }
        #endregion Setup & TearDown methods

        #region Private methods
        private string ExtractResourceToLocalFile(string resourceName, string assemblyName, string filePath = null)
        {
            if (string.IsNullOrEmpty(filePath))
                filePath = Path.GetTempFileName();

            var assembly = GetAssemblyByName(assemblyName);

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (MemoryStream reader = new MemoryStream())
            {
                stream.CopyTo(reader);
                File.WriteAllBytes(filePath, reader.ToArray());
            }

            return filePath;
        }

        private Assembly GetAssemblyByName(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == name);
        }

        private T DeserializeJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        #endregion Private methods
    }
}
