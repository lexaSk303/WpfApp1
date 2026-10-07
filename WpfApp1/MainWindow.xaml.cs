using Microsoft.Data.Sqlite;
using Npgsql;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private const string NpgsqlConnectionString =
            "Host=wotacoma.ru;Port=5090;Username=lexask333;Password=123SLexaS321;Database=lexask333;";
        private int _currentStep = 0;
        private string _pendingAction = "";
        private string _pendingId = "";

        public MainWindow()
        {
            InitializeComponent();
            InitializeDatabaseAsync();
            AppendOutput("Добро пожаловать в SPACE STATION CONTROL\n");
            AppendOutput("Введите номер пункта меню:\n");
        }

        private async void InitializeDatabaseAsync()
        {
            using var connection = new NpgsqlConnection(NpgsqlConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS crew (
                    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    role VARCHAR(50) NOT NULL,
                    health INTEGER NOT NULL CHECK (health BETWEEN 0 AND 100),
                    status VARCHAR(30) NOT NULL
                );

                CREATE TABLE IF NOT EXISTS station_systems (
                    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    status VARCHAR(30) NOT NULL
                );

                DELETE FROM crew;
                DELETE FROM station_systems;

                INSERT INTO crew (name, role, health, status) VALUES
                    ('Алексей Григорун', 'Командир', 100, 'На станции'),
                    ('Наташа Петрова', 'Инженер', 85, 'На станции'),
                    ('Дима Маслеников', 'Пилот', 92, 'На станции'),
                    ('Анна Матюшева', 'Медик', 100, 'На станции'),
                    ('Миша Траткин', 'Инженер', 73, 'На станции');

                INSERT INTO station_systems (name, status) VALUES
                    ('Двигатели', 'OK'),
                    ('Жизнеобеспечение', 'OK'),
                    ('Навигация', 'WARNING'),
                    ('Связь', 'OK');
            ";
            await command.ExecuteNonQueryAsync();
        }
        private async void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            string input = InputBox.Text.Trim();
            InputBox.Clear();
            AppendOutput($"> {input}\n");

            if (_currentStep > 0)
            {
                await HandleMultiStepInputAsync(input);
                return;
            }

            switch (input)
            {
                case "1":
                    await ShowCrewAsync();
                    break;
                case "2":
                    StartSearchCrew();
                    break;
                case "3":
                    StartModifyCrew();
                    break;
                case "4":
                    await FullDiagnosticsAsync();
                    break;
                case "5":
                    await EmergencyModeAsync();
                    break;
                case "0":
                    Application.Current.Shutdown();
                    break;
                default:
                    AppendOutput("Неверный выбор. Попробуйте снова.\n");
                    break;
            }
        }

        private async Task ShowCrewAsync()
        {
            var sb = new StringBuilder();

            using var connection = new NpgsqlConnection(NpgsqlConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, name, role, health, status
                FROM crew
                ORDER BY id;
            ";

            using var reader = await command.ExecuteReaderAsync();

            sb.AppendLine("╔════╦══════════════════╦═══════════════════╦══════════╦═══════════════╗");
            sb.AppendLine("║ ID ║ Имя              ║ Роль              ║ Здоровье ║ Статус        ║");
            sb.AppendLine("╠════╬══════════════════╬═══════════════════╬══════════╬═══════════════╣");

            while (await reader.ReadAsync())
            {
                int id = reader.GetInt32(0);
                string name = reader.GetString(1);
                string role = reader.GetString(2);
                int health = reader.GetInt32(3);
                string status = reader.GetString(4);

                sb.AppendLine($"║ {id,-2} ║ {name,-16} ║ {role,-17} ║ {health,-8} ║ {status,-13} ");
            }

            sb.AppendLine("╚════╩══════════════════╩═══════════════════╩══════════╩═══════════════╝");

            AppendOutput(sb.ToString());
        }
        private void StartSearchCrew()
        {
            _currentStep = 1;
            PromptLabel.Text = "Введите имя: > ";
        }

        private async Task SearchCrewAsync(string name)
        {
            var sb = new StringBuilder();

            using var connection = new NpgsqlConnection(NpgsqlConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT id, name, role, health, status
                FROM crew
                WHERE name ILIKE @name;
            ";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@name";
            parameter.Value = $"%{name}%";
            command.Parameters.Add(parameter);

            using var reader = await command.ExecuteReaderAsync();

            sb.AppendLine("╔══════════════════════╦═══════════════════╦══════════╦═══════════════╗");
            sb.AppendLine("║ ID ║ Имя              ║ Роль              ║ Здоровье ║ Статус        ║");
            sb.AppendLine("╠════╬══════════════════╬═══════════════════╬══════════╬═══════════════╣");

            bool found = false;
            while (await reader.ReadAsync())
            {
                found = true;
                int id = reader.GetInt32(0);
                string crewName = reader.GetString(1);
                string role = reader.GetString(2);
                int health = reader.GetInt32(3);
                string status = reader.GetString(4);

                sb.AppendLine($"║ {id,-2} ║ {crewName,-16} ║ {role,-17} ║ {health,-8} ║ {status,-13} ║");
            }

            if (!found)
            {
                sb.AppendLine("║ Никого не найдено                                                  ║");
            }

            sb.AppendLine("╚════╩══════════════════╩═══════════════════╩══════════╩═══════════════╝");

            AppendOutput(sb.ToString());
        }

        private void StartModifyCrew()
        {
            _currentStep = 2;
            AppendOutput("Меню изменения состояния:\n");
            AppendOutput("1. Нанести урон\n");
            AppendOutput("2. Восстановить здоровье\n");
            AppendOutput("3. Изменить статус\n");
            AppendOutput("4. Назад\n");
            PromptLabel.Text = "Выберите действие: > ";
        }

        private async Task HandleModifyCrewAsync(string input)
        {
            switch (_currentStep)
            {
                case 2:
                    if (input == "4")
                    {
                        _currentStep = 0;
                        PromptLabel.Text = "Выберите действие: > ";
                        return;
                    }
                    if (input != "1" && input != "2" && input != "3")
                    {
                        AppendOutput("Неверный выбор.\n");
                        return;
                    }
                    _pendingAction = input;
                    _currentStep = 3;
                    PromptLabel.Text = "Введите ID: > ";
                    break;

                case 3:
                    _pendingId = input;
                    _currentStep = 4;
                    if (_pendingAction == "3")
                        PromptLabel.Text = "Введите новый статус: > ";
                    else
                        PromptLabel.Text = "Введите значение: > ";
                    break;

                case 4:
                    await ApplyModificationAsync(_pendingAction, _pendingId, input);
                    _currentStep = 0;
                    PromptLabel.Text = "Выберите действие: > ";
                    break;
            }
        }

        private async Task ApplyModificationAsync(string action, string idStr, string valueStr)
        {
            var sb = new StringBuilder();

            using var connection = new NpgsqlConnection(NpgsqlConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();

            if (!int.TryParse(idStr, out int id))
            {
                sb.AppendLine("Неверный ID.");
                AppendOutput(sb.ToString());
                return;
            }

            command.CommandText = "SELECT name, health, status FROM crew WHERE id = @id;";
            var idParam = command.CreateParameter();
            idParam.ParameterName = "@id";
            idParam.Value = id;
            command.Parameters.Add(idParam);

            using var reader = await command.ExecuteReaderAsync();
            string name = "";
            int oldHealth = 0;
            string oldStatus = "";

            if (await reader.ReadAsync())
            {
                name = reader.GetString(0);
                oldHealth = reader.GetInt32(1);
                oldStatus = reader.GetString(2);
            }
            else
            {
                sb.AppendLine($"Член экипажа с ID {id} не найден.");
                AppendOutput(sb.ToString());
                return;
            }

            reader.Close();
            command.Parameters.Clear();

            if (action == "1") 
            {
                if (!int.TryParse(valueStr, out int damage))
                {
                    sb.AppendLine("Неверное значение урона.");
                }
                else
                {
                    command.CommandText = @"
                        UPDATE crew
                        SET health = GREATEST(health - @damage, 0)
                        WHERE id = @id;
                    ";
                    var dmgParam = command.CreateParameter();
                    dmgParam.ParameterName = "@damage";
                    dmgParam.Value = damage;
                    command.Parameters.Add(dmgParam);

                    var idP = command.CreateParameter();
                    idP.ParameterName = "@id";
                    idP.Value = id;
                    command.Parameters.Add(idP);

                    await command.ExecuteNonQueryAsync();

                    int newHealth = Math.Max(oldHealth - damage, 0);
                    sb.AppendLine($"{name}\nЗдоровье:\n{oldHealth} → {newHealth}\n\nОперация выполнена.");
                }
            }
            else if (action == "2")
            {
                if (!int.TryParse(valueStr, out int heal))
                {
                    sb.AppendLine("Неверное значение.");
                }
                else
                {
                    command.CommandText = @"
                        UPDATE crew
                        SET health = LEAST(health + @heal, 100)
                        WHERE id = @id;
                    ";
                    var healParam = command.CreateParameter();
                    healParam.ParameterName = "@heal";
                    healParam.Value = heal;
                    command.Parameters.Add(healParam);

                    var idP = command.CreateParameter();
                    idP.ParameterName = "@id";
                    idP.Value = id;
                    command.Parameters.Add(idP);

                    await command.ExecuteNonQueryAsync();

                    int newHealth = Math.Min(oldHealth + heal, 100);
                    sb.AppendLine($"{name}\nЗдоровье:\n{oldHealth} → {newHealth}\n\nОперация выполнена.");
                }
            }
            else if (action == "3") 
            {
                command.CommandText = "UPDATE crew SET status = @status WHERE id = @id;";

                var statusParam = command.CreateParameter();
                statusParam.ParameterName = "@status";
                statusParam.Value = valueStr;
                command.Parameters.Add(statusParam);

                var idP = command.CreateParameter();
                idP.ParameterName = "@id";
                idP.Value = id;
                command.Parameters.Add(idP);

                await command.ExecuteNonQueryAsync();

                sb.AppendLine($"{name}\nСтатус:\n{oldStatus} → {valueStr}\n\nОперация выполнена.");
            }

            AppendOutput(sb.ToString());
        }
        private async Task<string> CheckCrewAsync()
        {
            await Task.Delay(500);

            using var connection = new NpgsqlConnection(NpgsqlConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM crew WHERE health > 0;";

            var count = await command.ExecuteScalarAsync();

            return $"Экипаж: OK ({count} чел. активно)";
        }

        private async Task<string> CheckSystemsAsync()
        {
            await Task.Delay(700);

            using var connection = new NpgsqlConnection(NpgsqlConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT name, status FROM station_systems;";

            using var reader = await command.ExecuteReaderAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Системы:");
            while (await reader.ReadAsync())
            {
                string name = reader.GetString(0);
                string status = reader.GetString(1);
                sb.AppendLine($"  {name,-20} {status}");
            }

            return sb.ToString().TrimEnd();
        }

        private async Task<string> CheckCommunicationAsync()
        {
            var sw = Stopwatch.StartNew();

            using var connection = new NpgsqlConnection(NpgsqlConnectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT NOW();";
            await command.ExecuteScalarAsync();

            sw.Stop();

            return $"Связь: OK (задержка {sw.ElapsedMilliseconds} мс)";
        }

        private async Task FullDiagnosticsAsync()
        {
            AppendOutput("\n");
            AppendOutput("┌─────────────────────────────┐\n");
            AppendOutput("│      ДИАГНОСТИКА            │\n");
            AppendOutput("├─────────────────────────────┤\n");
            AppendOutput("│ Экипаж           ████████   │\n");
            AppendOutput("│ Двигатели        ████████   │\n");
            AppendOutput("│ Связь            ████████   │\n");
            AppendOutput("└─────────────────────────────┘\n\n");
            AppendOutput("Запуск диагностики...\n");

            Task<string> crewTask = CheckCrewAsync();
            Task<string> systemsTask = CheckSystemsAsync();
            Task<string> communicationTask = CheckCommunicationAsync();

            await Task.WhenAll(crewTask, systemsTask, communicationTask);

            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════╗");
            sb.AppendLine("║         РЕЗУЛЬТАТ ДИАГНОСТИКИ        ║");
            sb.AppendLine("╠══════════════════════════════════════╣");
            sb.AppendLine($"║  {crewTask.Result,-36} ║");
            sb.AppendLine($"║  {systemsTask.Result,-36} ║");
            sb.AppendLine($"║  {communicationTask.Result,-36} ║");
            sb.AppendLine("╚══════════════════════════════════════╝");
            sb.AppendLine("\nСтанция работоспособна.");

            AppendOutput(sb.ToString());
        }

        private async Task EmergencyModeAsync()
        {
            AppendOutput("\n⚠ ВНИМАНИЕ! АВАРИЙНЫЙ РЕЖИМ!\n");
            AppendOutput("Проверка систем...\n\n");

            Task<string> crewTask = CheckCrewAsync();
            Task<string> systemsTask = CheckSystemsAsync();
            Task<string> communicationTask = CheckCommunicationAsync();

            await Task.WhenAll(crewTask, systemsTask, communicationTask);

            var sb = new StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════╗");
            sb.AppendLine("║         РЕЗУЛЬТАТ ДИАГНОСТИКИ        ║");
            sb.AppendLine("══════════════════════════════════════╣");
            sb.AppendLine($"║  {crewTask.Result,-36} ║");
            sb.AppendLine($"║  {systemsTask.Result,-36} ║");
            sb.AppendLine($"║  {communicationTask.Result,-36} ║");
            sb.AppendLine("╚══════════════════════════════════════╝");
            sb.AppendLine("\nСтанция работоспособна.");

            AppendOutput(sb.ToString());
        }

        private async Task HandleMultiStepInputAsync(string input)
        {
            switch (_currentStep)
            {
                case 1:
                    _currentStep = 0;
                    PromptLabel.Text = "Выберите действие: > ";
                    await SearchCrewAsync(input);
                    break;

                case 2:
                case 3:
                case 4:
                    await HandleModifyCrewAsync(input);
                    break;
            }
        }
        private void AppendOutput(string text)
        {
            OutputBox.AppendText(text);
            OutputBox.ScrollToEnd();
        }
    }
}