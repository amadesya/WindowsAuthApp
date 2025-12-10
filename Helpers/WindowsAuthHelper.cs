using System;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;

namespace WindowsAuthApp.Helpers
{
    public static class WindowsAuthHelper
    {
        /// <summary>
        /// Проверка учетных данных Windows
        /// </summary>
        public static bool ValidateCredentials(string domain, string username, string password)
        {
            using (PrincipalContext context = new PrincipalContext(ContextType.Domain, domain))
            {
                return context.ValidateCredentials(username, password);
            }
        }

        /// <summary>
        /// Получение информации о текущем пользователе Windows
        /// </summary>
        public static WindowsUserInfo GetCurrentWindowsUserInfo()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            return new WindowsUserInfo
            {
                Name = identity.Name,
                AuthenticationType = identity.AuthenticationType,
                IsAuthenticated = identity.IsAuthenticated,
                IsSystem = identity.IsSystem,
                IsGuest = identity.IsGuest,
                IsAnonymous = identity.IsAnonymous,
                UserSid = identity.User?.Value,
                IsAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator)
            };
        }

        /// <summary>
        /// Получение информации о пользователе Active Directory
        /// </summary>
        public static ADUserInfo GetADUserInfo(string domain, string username)
        {
            try
            {
                using (PrincipalContext context = new PrincipalContext(ContextType.Domain, domain))
                using (UserPrincipal user = UserPrincipal.FindByIdentity(context, username))
                {
                    if (user != null)
                    {
                        return new ADUserInfo
                        {
                            DisplayName = user.DisplayName,
                            SamAccountName = user.SamAccountName,
                            Email = user.EmailAddress,
                            Description = user.Description,
                            IsLockedOut = user.IsAccountLockedOut(),
                            LastLogon = user.LastLogon,
                            PasswordNeverExpires = user.PasswordNeverExpires,
                            UserCannotChangePassword = user.UserCannotChangePassword
                        };
                    }
                }
            }
            catch (Exception)
            {
                // Логирование ошибки
            }

            return null;
        }

        /// <summary>
        /// Проверка, является ли пользователь членом группы
        /// </summary>
        public static bool IsUserInGroup(string groupName)
        {
            WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            return principal.IsInRole(groupName);
        }
    }

    public class WindowsUserInfo
    {
        public string Name { get; set; }
        public string AuthenticationType { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool IsSystem { get; set; }
        public bool IsGuest { get; set; }
        public bool IsAnonymous { get; set; }
        public string UserSid { get; set; }
        public bool IsAdmin { get; set; }
    }

    public class ADUserInfo
    {
        public string DisplayName { get; set; }
        public string SamAccountName { get; set; }
        public string Email { get; set; }
        public string Description { get; set; }
        public bool IsLockedOut { get; set; }
        public DateTime? LastLogon { get; set; }
        public bool PasswordNeverExpires { get; set; }
        public bool UserCannotChangePassword { get; set; }
    }
}