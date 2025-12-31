using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace v232.Launcher.WPF.Services
{
    public class RegisterService
    {
        public Dictionary<int, string> SignUpInputMessages = new Dictionary<int, string>
        {
            { 1, "Username must be Alpha Numeric and between 6 - 18 characters long." },
            { 2, "Password must contain letters, numbers and special characters and be between 6 - 18 characters long." },
            { 3, "Your passwords must match. Please try again." },
            { 10, "Email is invalid." },
            { 20, "Please use a different username." },
            { 21, "Please use a different password." },
            { 22, "Please use a different e-mail." }
        };

        /// <summary>
        /// Validates sign up input
        /// Returns 0 if valid, error code otherwise
        /// </summary>
        public int HandleSignUpInput(string username, string password, string confirmPassword, string email)
        {
            if (username.Length < 6 || username.Length > 18 || !IsAlphaNumeric(username))
                return 1;
            else if (password.Length < 6 || password.Length > 18 || !IsValidPassword(password))
                return 2;
            else if (password != confirmPassword)
                return 3;
            else if (string.IsNullOrEmpty(email) || !IsValidEmail(email))
                return 10;

            // Check for SQL injection
            if (!IsSafeFromSqlInjection(username))
                return 20;
            if (!IsSafeFromSqlInjection(password))
                return 21;
            if (!IsSafeFromSqlInjection(email))
                return 22;

            return 0;
        }

        /// <summary>
        /// Gets error message for validation code
        /// </summary>
        public string GetErrorMessage(int code)
        {
            return SignUpInputMessages.ContainsKey(code) ? SignUpInputMessages[code] : "Unknown error";
        }

        private bool IsAlphaNumeric(string input)
        {
            return Regex.IsMatch(input, "^[a-zA-Z0-9]+$");
        }

        private bool IsValidPassword(string input)
        {
            return Regex.IsMatch(input, "^[a-zA-Z0-9!@#$%^&*()-_+=<>?]+$");
        }

        public bool IsValidEmail(string emailaddress)
        {
            try
            {
                if (!Regex.IsMatch(emailaddress, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,4}$"))
                    return false;

                MailAddress mailAddress = new MailAddress(emailaddress);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private bool IsSafeFromSqlInjection(string input)
        {
            string lowercaseInput = input.ToLower();
            string[] sqlKeywords = { "select", "update", "delete", "drop", "alter", "insert", "union", "exec" };

            foreach (string keyword in sqlKeywords)
            {
                if (lowercaseInput.Contains(keyword))
                    return false;
            }

            if (input.Contains(";"))
                return false;

            string[] commentSequences = { "--", "/*", "*/" };

            foreach (string sequence in commentSequences)
            {
                if (input.Contains(sequence))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Creates account via server
        /// </summary>
        public async Task<(bool success, string message)> CreateAccount(string username, string password, string email, Client client)
        {
            byte request = await Handlers.SendAccountCreateRequest(username, password, email, client);

            switch (request)
            {
                case 0:
                    return (true, "Account successfully created!");
                case 1:
                    return (false, "Account name already taken!");
                case 2:
                    return (false, "This IP has already created an account!");
                case 3:
                    return (false, "This Mac Address has already created an account!");
                case 4:
                default:
                    return (false, "Unknown Error: Client and Server info are mismatched.");
            }
        }
    }
}
