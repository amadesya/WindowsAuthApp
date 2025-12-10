using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using System.ComponentModel;

namespace WindowsAuthApp
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private DispatcherTimer timer;
        private List<string> logEntries = new List<string>();
        private DateTime currentDateTime;

        public event PropertyChangedEventHandler PropertyChanged;

        public DateTime CurrentDateTime
        {
            get => currentDateTime;
            set
            {
                currentDateTime = value;
                OnPropertyChanged(nameof(CurrentDateTime));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadCurrentWindowsUser();
            InitializeTimer();
            AddLog("Приложение запущено");
        }

        private void InitializeTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, e) => CurrentDateTime = DateTime.Now;
            timer.Start();
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void LoadCurrentWindowsUser()
        {
            try
            {
                WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(currentUser);

                string userInfo = $"Имя пользователя: {currentUser.Name}\n" +
                                 $"Тип аутентификации: {currentUser.AuthenticationType}\n" +
                                 $"Аутентифицирован: {currentUser.IsAuthenticated}\n" +
                                 $"Анонимный: {currentUser.IsAnonymous}\n" +
                                 $"Гость: {currentUser.IsGuest}\n" +
                                 $"Системный: {currentUser.IsSystem}\n" +
                                 $"Администратор: {principal.IsInRole(WindowsBuiltInRole.Administrator)}";

                CurrentUserText.Text = userInfo;

                AddLog($"Текущий пользователь Windows: {currentUser.Name}");
                StatusBarText.Text = $"Текущий пользователь: {currentUser.Name}";
            }
            catch (Exception ex)
            {
                CurrentUserText.Text = $"Ошибка: {ex.Message}";
                AddLog($"Ошибка при получении информации о пользователе: {ex.Message}");
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string domain = DomainTextBox.Text;
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Введите имя пользователя", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Если домен не указан, используем локальный компьютер
                ContextType contextType = string.IsNullOrEmpty(domain) ?
                    ContextType.Machine : ContextType.Domain;

                using (PrincipalContext context = new PrincipalContext(contextType, domain))
                {
                    bool isValid = context.ValidateCredentials(username, password);

                    if (isValid)
                    {
                        AuthResultText.Text = "Аутентификация успешна!";
                        AuthResultText.Foreground = System.Windows.Media.Brushes.Green;

                        // Получаем информацию о пользователе
                        UserPrincipal user = UserPrincipal.FindByIdentity(context, username);
                        DisplayUserDetails(user);

                        string logMessage = string.IsNullOrEmpty(domain) ?
                            $"Успешная аутентификация: {username}" :
                            $"Успешная аутентификация: {domain}\\{username}";

                        AddLog(logMessage);
                        StatusBarText.Text = $"Аутентифицирован: {username}";
                    }
                    else
                    {
                        AuthResultText.Text = "Неверные учетные данные";
                        AuthResultText.Foreground = System.Windows.Media.Brushes.Red;
                        UserDetailsText.Text = "Ошибка аутентификации";

                        string logMessage = string.IsNullOrEmpty(domain) ?
                            $"Ошибка аутентификации для: {username}" :
                            $"Ошибка аутентификации для: {domain}\\{username}";

                        AddLog(logMessage);
                        StatusBarText.Text = "Ошибка аутентификации";
                    }
                }
            }
            catch (PrincipalServerDownException)
            {
                AuthResultText.Text = "Сервер домена недоступен";
                AuthResultText.Foreground = System.Windows.Media.Brushes.Red;
                AddLog("Ошибка: сервер домена недоступен");
            }
            catch (PrincipalException ex)
            {
                AuthResultText.Text = $"Ошибка AD: {ex.Message}";
                AuthResultText.Foreground = System.Windows.Media.Brushes.Red;
                AddLog($"Ошибка AD: {ex.Message}");
            }
            catch (Exception ex)
            {
                AuthResultText.Text = $"Ошибка: {ex.Message}";
                AuthResultText.Foreground = System.Windows.Media.Brushes.Red;
                UserDetailsText.Text = ex.ToString();
                AddLog($"Исключение при аутентификации: {ex.Message}");
            }
        }

        private void WindowsLoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Используем текущие учетные данные Windows
                WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(currentUser);

                AuthResultText.Text = $"Аутентификация Windows успешна!";
                AuthResultText.Foreground = System.Windows.Media.Brushes.Green;

                // Пытаемся получить информацию из AD
                try
                {
                    string[] nameParts = currentUser.Name.Split('\\');
                    string domainName = nameParts.Length > 1 ? nameParts[0] : Environment.MachineName;
                    string userName = nameParts.Length > 1 ? nameParts[1] : nameParts[0];

                    ContextType contextType = currentUser.Name.Contains("\\") ?
                        ContextType.Domain : ContextType.Machine;

                    using (PrincipalContext context = new PrincipalContext(contextType, domainName))
                    {
                        UserPrincipal user = UserPrincipal.FindByIdentity(context, userName);

                        if (user != null)
                        {
                            DisplayUserDetails(user);
                        }
                        else
                        {
                            // Если не нашли в AD, показываем базовую информацию
                            UserDetailsText.Text = $"Имя: {currentUser.Name}\n" +
                                                  $"Аутентификация: {currentUser.AuthenticationType}\n" +
                                                  $"SID: {currentUser.User?.Value}\n" +
                                                  $"Администратор: {principal.IsInRole(WindowsBuiltInRole.Administrator)}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Если не удалось получить из AD, показываем Windows информацию
                    UserDetailsText.Text = $"Имя: {currentUser.Name}\n" +
                                          $"Аутентификация: {currentUser.AuthenticationType}\n" +
                                          $"SID: {currentUser.User?.Value}\n" +
                                          $"Администратор: {principal.IsInRole(WindowsBuiltInRole.Administrator)}\n" +
                                          $"Ошибка AD: {ex.Message}";
                }

                AddLog($"Аутентификация как текущий пользователь: {currentUser.Name}");
                StatusBarText.Text = $"Используется: {currentUser.Name}";
            }
            catch (Exception ex)
            {
                AuthResultText.Text = $"Ошибка: {ex.Message}";
                AuthResultText.Foreground = System.Windows.Media.Brushes.Red;
                AddLog($"Ошибка при Windows-аутентификации: {ex.Message}");
            }
        }

        private void CheckAdminButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

                if (isAdmin)
                {
                    MessageBox.Show("Текущий пользователь имеет права администратора",
                        "Проверка прав", MessageBoxButton.OK, MessageBoxImage.Information);
                    AddLog("Проверка прав: пользователь администратор");
                }
                else
                {
                    MessageBox.Show("Текущий пользователь не имеет прав администратора",
                        "Проверка прав", MessageBoxButton.OK, MessageBoxImage.Warning);
                    AddLog("Проверка прав: пользователь не администратор");
                }

                // Проверка других ролей
                CheckUserRoles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке прав: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"Ошибка проверки прав: {ex.Message}");
            }
        }

        private void CheckUserRoles()
        {
            try
            {
                WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                string rolesInfo = "Роли пользователя:\n";

                // Проверка стандартных ролей
                rolesInfo += $"Администратор: {principal.IsInRole(WindowsBuiltInRole.Administrator)}\n";
                rolesInfo += $"Пользователь: {principal.IsInRole(WindowsBuiltInRole.User)}\n";
                rolesInfo += $"Гость: {principal.IsInRole(WindowsBuiltInRole.Guest)}\n";
                rolesInfo += $"Оператор архива: {principal.IsInRole(WindowsBuiltInRole.BackupOperator)}\n";
                rolesInfo += $"Оператор печати: {principal.IsInRole(WindowsBuiltInRole.PrintOperator)}\n";
                rolesInfo += $"Power User: {principal.IsInRole(WindowsBuiltInRole.PowerUser)}";

                AddLog($"Роли определены: {rolesInfo.Replace("\n", "; ")}");
            }
            catch (Exception ex)
            {
                AddLog($"Ошибка при определении ролей: {ex.Message}");
            }
        }

        private void DisplayUserDetails(UserPrincipal user)
        {
            if (user != null)
            {
                string details = $"Полное имя: {user.DisplayName ?? "Не указано"}\n";
                details += $"Имя пользователя: {user.SamAccountName}\n";
                details += $"Email: {user.EmailAddress ?? "Не указан"}\n";
                details += $"Описание: {user.Description ?? "Не указано"}\n";
                details += $"Учетная запись активна: {(user.Enabled.HasValue ? user.Enabled.Value.ToString() : "Неизвестно")}\n";
                details += $"Учетная запись заблокирована: {user.IsAccountLockedOut()}\n";
                details += $"Пароль никогда не истекает: {user.PasswordNeverExpires}\n";
                details += $"Пользователь не может менять пароль: {user.UserCannotChangePassword}\n";
                details += $"Последний вход: {(user.LastLogon.HasValue ? user.LastLogon.Value.ToString("dd.MM.yyyy HH:mm") : "Неизвестно")}\n";
                details += $"Последнее изменение пароля: {(user.LastPasswordSet.HasValue ? user.LastPasswordSet.Value.ToString("dd.MM.yyyy HH:mm") : "Неизвестно")}";

                UserDetailsText.Text = details;
            }
            else
            {
                UserDetailsText.Text = "Не удалось получить детальную информацию о пользователе";
            }
        }

        private void AddLog(string message)
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            logEntries.Add(logEntry);

            // Ограничиваем количество записей в логе
            if (logEntries.Count > 50)
                logEntries.RemoveAt(0);

            LogListBox.ItemsSource = null;
            LogListBox.ItemsSource = logEntries;

            // Прокручиваем к последней записи
            if (LogListBox.Items.Count > 0)
                LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
        }
    }
}