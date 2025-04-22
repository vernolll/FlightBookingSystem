using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;

namespace FlightBookingSystem
{
    public partial class SeatSelectionWindow : Window
    {
        private int _flightId;
        private int _userId;
        private TcpClient _client; // Ссылка на существующий TcpClient
        private NetworkStream _stream; // Поток для чтения и записи
        private StreamWriter _writer; // Общий StreamWriter
        private StreamReader _reader; // Общий StreamReader

        public SeatSelectionWindow(int flightId, int userId, TcpClient client)
        {
            InitializeComponent();
            _flightId = flightId;
            _userId = userId;
            _client = client;

            try
            {
                // Инициализация потоков
                _stream = _client.GetStream();
                _writer = new StreamWriter(_stream, Encoding.ASCII, leaveOpen: true);
                _reader = new StreamReader(_stream, Encoding.ASCII, leaveOpen: true);

                LoadSeats(); // Загрузка доступных мест
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации потоков: {ex.Message}");
            }
        }
       

    private async void LoadSeats()
        {
            try
            {
                if (_client == null || !_client.Connected)
                {
                    MessageBox.Show("Ошибка подключения к серверу.");
                    return;
                }

                // Отправляем запрос на получение доступных мест
                var availableSeats = await SendRequestToServer($"GET_SEATS {_flightId}");

                if (string.IsNullOrEmpty(availableSeats))
                {
                    MessageBox.Show("Ответ от сервера пустой.");
                    return;
                }

                if (availableSeats.StartsWith("Seats:"))
                {
                    availableSeats = availableSeats.Substring("Seats:".Length).Trim();
                }

                // Разбираем ответ, чтобы извлечь номера мест и их цены
                string[] seatsData = availableSeats.Split(new[] { "], [" }, StringSplitOptions.RemoveEmptyEntries)
                                                   .Select(seat => seat.Trim(new char[] { '[', ']' }))
                                                   .ToArray();

                if (seatsData.Length == 0)
                {
                    MessageBox.Show("Нет доступных мест для выбранного рейса.");
                    return;
                }

                // Создаем список, в котором будет содержаться строковое представление для каждого места с ценой
                var formattedSeats = new List<string>();

                foreach (var seat in seatsData)
                {
                    // Пример формата: 1A-Price:158,00
                    var parts = seat.Split(new string[] { "-Price:" }, StringSplitOptions.None);

                    if (parts.Length == 2)
                    {
                        var seatNumber = parts[0].Trim();
                        var priceStr = parts[1].Trim();

                        // Заменяем запятую на точку, чтобы корректно преобразовать цену в число
                        priceStr = priceStr.Replace(",", ".");

                        // Преобразуем цену в число (decimal)
                        if (decimal.TryParse(priceStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal price))
                        {
                            // Форматируем цену в долларах, используя культуру США (en-US)
                            var formattedPrice = price.ToString("C2", new CultureInfo("en-US")); // C2 означает валютный формат с двумя знаками после запятой

                            formattedSeats.Add($"Seat: {seatNumber} - Price: {formattedPrice}");
                        }
                        else
                        {
                            formattedSeats.Add($"Seat: {seatNumber} - Error parsing price");
                        }
                    }
                    else
                    {
                        // Если формат некорректный, выводим ошибку
                        formattedSeats.Add("Ошибка форматирования данных.");
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    SeatsListBox.ItemsSource = formattedSeats;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }


    private async Task<string> SendRequestToServer(string request)
        {
            try
            {
                if (!_stream.CanWrite)
                {
                    MessageBox.Show("Поток недоступен для записи.");
                    return string.Empty;
                }

                await _writer.WriteLineAsync(request);
                await _writer.FlushAsync();

                string response = await _reader.ReadLineAsync();
                if (string.IsNullOrEmpty(response))
                {
                    MessageBox.Show("Сервер не отправил ответ.");
                    return string.Empty;
                }

                return response;
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Ошибка при чтении данных: {ex.Message}");
                return string.Empty;
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Ошибка сокета: {ex.Message}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Неизвестная ошибка: {ex.Message}");
                return string.Empty;
            }
        }

        private async void ConfirmSeatButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedSeat = SeatsListBox.SelectedItem as string;
            if (!string.IsNullOrEmpty(selectedSeat))
            {
                // Разбираем строку с местом и ценой
                var parts = selectedSeat.Split(new string[] { " - Price: " }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    string seat = parts[0].Trim();  // Получаем номер места
                    string priceStr = parts[1].Trim();  // Получаем цену (для дальнейшего использования)

                    // Убираем префикс "Seat: ", если он есть
                    seat = seat.Replace("Seat: ", "").Trim();

                    // Забронировать место на сервере
                    bool success = await BookSeatOnServer(_flightId, seat);
                    if (success)
                    {
                        var result = MessageBox.Show("Место забронировано успешно! Хотите получить билет по почте?", "Подтверждение по почте", MessageBoxButton.YesNo);
                        if (result == MessageBoxResult.Yes)
                        {
                            SeatsListBox.Visibility = Visibility.Collapsed;
                            ConfirmSeatButton.Visibility = Visibility.Collapsed;
                            Choose.Visibility = Visibility.Collapsed;

                            EmailInputSection.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            this.Close();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Не удалось забронировать место. Попробуйте снова.");
                    }
                }
                else
                {
                    MessageBox.Show("Ошибка формата выбранного места. Попробуйте снова.");
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите место.");
            }
        }

        private async Task<bool> BookSeatOnServer(int flightId, string seat)
        {
            try
            {
                if (!_stream.CanWrite)
                {
                    MessageBox.Show("Поток недоступен для записи.");
                    return false;
                }

                // Отправляем запрос на бронирование места
                string request = $"BOOK_SEAT {flightId} {seat}";
                await _writer.WriteLineAsync(request);
                await _writer.FlushAsync();

                string response = await _reader.ReadLineAsync();
                if (string.IsNullOrEmpty(response))
                {
                    MessageBox.Show("Сервер не отправил ответ.");
                    return false;
                }

                // Ответ от сервера должен начинаться с "SUCCESS" для успешной операции
                return response.StartsWith("SUCCESS");
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Ошибка при чтении данных: {ex.Message}");
                return false;
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Ошибка сокета: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Неизвестная ошибка: {ex.Message}");
                return false;
            }
        }


        private void SendTicketButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text;

            if (string.IsNullOrEmpty(email))
            {
                MessageBox.Show("Пожалуйста, введите действующий адрес электронной почты.");
                return;
            }

            if (!IsValidEmail(email))
            {
                MessageBox.Show("Введенный адрес электронной почты недействителен. Пожалуйста, попробуйте снова.");
                return;
            }

            MessageBox.Show("Билет отправлен на вашу почту!");
            this.Close();
        }

        private bool IsValidEmail(string email)
        {
            var emailRegex = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
            return emailRegex.IsMatch(email);
        }

        private void CloseConnection()
        {
            _writer?.Close();
            _reader?.Close();
            _stream?.Close();
            _client?.Close();
        }

        ~SeatSelectionWindow()
        {
            CloseConnection();
        }
    }
}
