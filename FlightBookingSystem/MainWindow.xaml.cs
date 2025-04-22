using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace FlightBookingSystem
{
    public partial class MainWindow : Window
    {
        private int _currentUserId;
        private TcpClient _client;
        private NetworkStream _stream;
        private int _loginAttempts = 0;

        public MainWindow()
        {
            InitializeComponent();
            InitializeConnection();
        }

        private void InitializeConnection()
        {
            try
            {
                _client = new TcpClient("localhost", 8080); // Подключаемся к серверу
                _stream = _client.GetStream(); // Получаем поток
                Console.WriteLine("Соединение с сервером установлено.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось подключиться к серверу: {ex.Message}");
                Application.Current.Shutdown(); // Завершаем приложение, если подключение невозможно
            }
        }

        private void CloseConnection()
        {
            try
            {
                _stream?.Close();
                _client?.Close();
                Console.WriteLine("Соединение с сервером закрыто.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при закрытии соединения: {ex.Message}");
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            CloseConnection();
        }

        private string SendRequestToServer(string request)
        {
            try
            {
                if (_stream == null)
                {
                    throw new InvalidOperationException("Соединение с сервером не установлено.");
                }

                var requestBytes = Encoding.UTF8.GetBytes(request);
                _stream.Write(requestBytes, 0, requestBytes.Length);

                var buffer = new byte[1024];
                int bytesRead = _stream.Read(buffer, 0, buffer.Length);

                if (bytesRead > 0)
                {
                    var response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine("Response: " + response);
                    return response;
                }
                else
                {
                    MessageBox.Show("Ошибка: сервер не ответил.");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отправке запроса: {ex.Message}");
                return string.Empty;
            }
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Пожалуйста, заполните все поля.");
                return;
            }

            var response = SendRequestToServer($"LOGIN {username} {password}");

            if (response.StartsWith("SUCCESS"))
            {
                _currentUserId = int.Parse(response.Substring("SUCCESS=".Length));
                AuthGrid.Visibility = Visibility.Collapsed;
                TicketsPage.Visibility = Visibility.Visible;
                ProfileButton.Visibility = Visibility.Visible;
                TicketsButton.Visibility = Visibility.Visible;
                LogoutButton.Visibility = Visibility.Visible;
                LoadTickets();
            }
            else
            {
                _loginAttempts++;
                MessageBox.Show("Неверный логин или пароль.");
                if (_loginAttempts >= 3)
                {
                    _loginAttempts = 0; // Сбросить счётчик попыток
                    var result = MessageBox.Show("Хотите сменить пароль?", "Смена пароля",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        AuthGrid.Visibility = Visibility.Collapsed;
                        ChangePasswordGrid.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void LoadTickets()
        {
            var response = SendRequestToServer("GET_FLIGHTS");

            if (string.IsNullOrEmpty(response))
            {
                MessageBox.Show("Нет ответа от сервера.");
                return;
            }

            var tickets = response.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line =>
                {
                    var parts = line.Split(new[] { ',' }, StringSplitOptions.None);
                    if (parts.Length >= 4)
                    {
                        var idPart = parts[0].Trim();
                        var idMatch = System.Text.RegularExpressions.Regex.Match(idPart, @"\d+");
                        var id = idMatch.Success ? int.Parse(idMatch.Value) : 0;

                        var fromPart = parts[1].Trim();
                        var toPart = parts[2].Trim();
                        var from = fromPart.Substring(fromPart.IndexOf(":") + 1).Trim();
                        var to = toPart.Substring(toPart.IndexOf(":") + 1).Trim();

                        var datePart = parts[3].Trim();
                        var dateString = datePart.Substring(datePart.IndexOf(":") + 1).Trim().Split(' ')[0];

                        DateTime date;
                        if (DateTime.TryParse(dateString, out date))
                        {
                            return new Flight
                            {
                                Id = id,
                                From = from,
                                To = to,
                                Date = date 
                            };
                        }
                        else
                        {
                            MessageBox.Show($"Invalid date: {dateString} in line: '{line}'", "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Error);
                            return null;
                        }
                    }
                    return null;
                })
                .Where(ticket => ticket != null)
                .ToList();

            if (tickets.Any())
            {
                TicketsDataGrid.ItemsSource = tickets;
                TicketsDataGrid.Items.Refresh();
            }
            else
            {
                MessageBox.Show("Нет доступных билетов.", "No Tickets", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            TicketsPage.Visibility = Visibility.Visible;
        }


        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            TicketsPage.Visibility = Visibility.Collapsed;
            AuthGrid.Visibility = Visibility.Visible;
            UsernameTextBox.Clear();
            PasswordBox.Clear();
            ProfileButton.Visibility = Visibility.Collapsed;
            TicketsButton.Visibility = Visibility.Collapsed;
            LogoutButton.Visibility = Visibility.Collapsed;
            ProfileGrid.Visibility = Visibility.Collapsed;
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var response = SendRequestToServer($"GET_USER {_currentUserId}");

            if (response.StartsWith("USER="))
            {
                var userInfo = response.Substring("USER=".Length).Split(',');

                if (userInfo.Length == 3)
                {
                    NameTextBox.Text = userInfo[0];
                    SurnameTextBox.Text = userInfo[1];
                    AgeTextBox.Text = userInfo[2];
                }
                else
                {
                    MessageBox.Show("Ошибка: Неверный формат данных пользователя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show($"Ошибка: {response}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            ProfileGrid.Visibility = Visibility.Visible;
            TicketsPage.Visibility = Visibility.Collapsed;
        }

        private void TicketsButton_Click(object sender, RoutedEventArgs e)
        {
            ProfileGrid.Visibility = Visibility.Collapsed;
            TicketsPage.Visibility = Visibility.Visible;
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            AuthGrid.Visibility = Visibility.Collapsed;
            RegisterGrid.Visibility = Visibility.Visible;
        }

        private void BackToLoginButton_Click(object sender, RoutedEventArgs e)
        {
            RegisterGrid.Visibility = Visibility.Collapsed;
            AuthGrid.Visibility = Visibility.Visible;
        }

        private void RegisterActionButton_Click(object sender, RoutedEventArgs e)
        {
            string username = RegisterUsernameTextBox.Text;
            string password = RegisterPasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                MessageBox.Show("Пожалуйста, заполните все поля.");
                return;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("Пароли не совпадают.");
                return;
            }

            if(!IsPasswordStrong(password))
            {
                MessageBox.Show("Ненадежный пароль. Пароль должен состоять не менее чем из 8 символов и содержать строчные и заглвыне буквы, а также числа.");
                return;
            }

            var response = SendRequestToServer($"REGISTER {username} {password}");

            if (response.StartsWith("SUCCESS"))
            {
                MessageBox.Show("Регистрация прошла успешно.");
                RegisterGrid.Visibility = Visibility.Collapsed;
                AuthGrid.Visibility = Visibility.Visible;
            }
            else
            {
                MessageBox.Show("Ошибка регистрации.");
            }
        }

        private void SaveChangesButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text;
            string surname = SurnameTextBox.Text;
            int age;

            if (int.TryParse(AgeTextBox.Text, out age))
            {
                if(age > 14 && age < 100)
                {
                    var response = SendRequestToServer($"UPDATE_USER {_currentUserId} {name} {surname} {age}");
                    if (response.StartsWith("SUCCESS"))
                    {
                        MessageBox.Show("Данные успешно сохранены!");
                        ProfileGrid.Visibility = Visibility.Collapsed;
                        TicketsPage.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        MessageBox.Show("Ошибка при сохранении данных.");
                    }
                }
                else
                {
                    MessageBox.Show("Введен недействительный возраст.");
                }
            }
            else
            {
                MessageBox.Show("Введите корректный возраст.");
            }
        }

        private void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            AuthGrid.Visibility = Visibility.Collapsed;
            ChangePasswordGrid.Visibility = Visibility.Visible;
        }

        private void BackToLoginFromChangePassword(object sender, RoutedEventArgs e)
        {
            ChangePasswordGrid.Visibility = Visibility.Collapsed;
            AuthGrid.Visibility = Visibility.Visible;
        }

        private void ChangePasswordActionButton_Click(object sender, RoutedEventArgs e)
        {
            string nickname = NicknameTextBox.Text;
            string newPassword = NewPasswordBox.Password;
            string confirmNewPassword = ConfirmNewPasswordBox.Password;

            if (string.IsNullOrEmpty(nickname) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmNewPassword))
            {
                MessageBox.Show("Пожалуйста, заполните все поля.");
                return;
            }

            if (newPassword != confirmNewPassword)
            {
                MessageBox.Show("Пароли не совпадают.");
                return;
            }

            if(!IsPasswordStrong(newPassword))
            {
                MessageBox.Show("Пароль небезопасен. Он должен быть не короче 8 символов, иметь большие и маленькие буквы, а также числа.");
                return;
            }

            var response = SendRequestToServer($"CHANGE_PASSWORD {nickname} {newPassword}");

            if (response.StartsWith("SUCCESS"))
            {
                MessageBox.Show("Пароль успешно изменен.");
                ChangePasswordGrid.Visibility = Visibility.Collapsed;
                AuthGrid.Visibility = Visibility.Visible;
            }
            else
            {
                MessageBox.Show("Ошибка изменения пароля.");
            }
        }

        public static bool IsPasswordStrong(string password)
        {
            // Минимальная длина пароля
            int minLength = 8;

            // Проверка длины
            if (password.Length < minLength)
                return false;

            // Флаги для проверки различных типов символов
            bool hasUpperCase = false;
            bool hasLowerCase = false;

            // Перебор символов пароля
            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUpperCase = true;
                else if (char.IsLower(c)) hasLowerCase = true;

                // Если все условия выполнены, пароль надёжный
                if (hasUpperCase && hasLowerCase )
                    return true;
            }

            // Пароль недостаточно сложный, если хотя бы одно условие не выполнено
            return false;
        }


        // Обработчик клика по кнопке "Купить билет"
        private void BuyButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;

            // Проверяем, что CommandParameter действительно установлен
            if (button.CommandParameter == null)
            {
                MessageBox.Show("CommandParameter не был установлен.");
                return;
            }

            int flightId;

            try
            {
                // Пробуем привести CommandParameter к нужному типу (int)
                flightId = (int)button.CommandParameter;
            }
            catch (InvalidCastException ex)
            {
                MessageBox.Show($"Ошибка при приведении типа CommandParameter: {ex.Message}");
                return;
            }

            // Теперь отправляем запрос или открываем окно выбора мест
            SeatSelectionWindow seatSelectionWindow = new SeatSelectionWindow(flightId, _currentUserId, _client);
            seatSelectionWindow.Show();
        }
    }

    public class Flight
    {
        public int Id { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public DateTime Date { get; set; }

        public string FormattedDate
        {
            get
            {
                return Date.ToString("yyyy-MM-dd");
            }
        }
    }

}
