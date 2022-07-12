using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using System.IO.Ports;
using System.IO;
using ThreadTimer = System.Threading.Timer;

namespace Praktika
{
    public partial class Form1 : Form
    {

        // Инициализация компонентов формы.
        public Form1()
        {
            InitializeComponent();
            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);
        }

        public string _db = "server=localhost;database=db;username=root;password=;charset=utf8mb4;";
        public string _select = "SELECT * FROM praktika_table"; 
        public string _insert = "INSERT INTO praktika_table (date, time, state, speed, current) VALUES (CURDATE(), CURTIME(), @state, @speed, @current)";
        public string _date, _time, _state;
        public int _speed, _current;
        public MySqlConnection _connection; // Для подключения к БД.
        public MySqlDataAdapter adapter_all, adapter_filter; // Для запросов вывода данных из таблицы.
        public MySqlCommand _command; // Ввод данных в таблицу.
        public DataTable _table; // Для DGV-таблицы.
        public SerialPort _port; // Для COM-порта.
        public DateTime _datetime; // Для даты и времени
        public ThreadTimer timer1;


        // Инициализация при запуске приложения
        private void Form1_Load(object sender, EventArgs e)
        {
            // Проверка работоспособности порта.
            _connection = new MySqlConnection(_db);
            _command = new MySqlCommand(_insert, _connection);
            try
            {
                _connection.Open();
                adapter_all = new MySqlDataAdapter(_select, _connection);
                _table = new DataTable();
                label10.Text = "подключена";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка подключения базы данных: " + Environment.NewLine + ex.Message);
            }

            try
            {
                _port = new SerialPort("COM7", 38400);
                _port.DataBits = 8;
                _port.Parity = Parity.None;
                _port.StopBits = StopBits.One;
                _port.Open();   // Тестовое открытие
                //timer1 = new ThreadTimer(timer1_Tick, null, 0, 1000); // Если успешно - запуск таймера.
                timer1 = new ThreadTimer(timer1_Tick, null, 1000, 1000); // Если успешно - запуск таймера.
            }
            catch
            {   // Не удалось подключиться к COM-порту - таймер не запускается,
                // вывод сообщения о том, что подключение не установлено.
                MessageBox.Show("COM-порт не подключен! " +
                    "Доступна только работа с базой данных. " +
                    "Проверьте соединение и перезапустите приложение!");
            }
            finally
            {
                if (_port.IsOpen)
                    _port.Close();  // и закрытие порта.
            }
        }

         // Обработка событий таймера на вывод БД
        // во время выполнения приложения.
        public void timer1_Tick(Object state)
        {
            if (_connection.State == ConnectionState.Open)
            {
                _port.Open();
                byte[] message = new byte[] { 0x01, 0x03, 0x21, 0x01, 0x00, 0x04 };
                byte[] crc = ModbusCRC16Calc(message);
                byte[] request = new byte[message.Length + crc.Length];
                message.CopyTo(request, 0);
                crc.CopyTo(request, request.Length - 2);
                _port.Write(request, 0, request.Length);
                byte[] response = ReadExact(_port.BaseStream, 13);
                _port.Close();
                int vfdState = MakeWord(response[3], response[4]);
                string stateString;
                if ((vfdState & 3) == 3)
                    stateString = "ON";
                else
                {
                    stateString = "OFF";
                }
                int speed = MakeWord(response[7], response[8]);
                int current = MakeWord(response[9], response[10]);

                Action a = () =>
                {
                    tbState.Text = stateString;
                    tbSpeed.Text = speed.ToString();
                    tbCurrent.Text = current.ToString();
                };
                this.Invoke(a);

                _datetime = DateTime.Now;
                _command.Parameters.Clear();
                _command.CommandText = _insert;
                _command.Parameters.AddWithValue("state", stateString);
                _command.Parameters.AddWithValue("speed", speed);
                _command.Parameters.AddWithValue("current", current);
                try
                {
                    _command.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Не удалось добавить данные! " + ex.Message);
                }
                
            }
            else
            {
                label10.Text = "отключена";
                MessageBox.Show("Подключение к базе данных прервано!" +
                    "Перезапустите программу!");
                timer1.Change(0, System.Threading.Timeout.Infinite);
            }
        }

        private int MakeWord(byte HB, byte LB)
        {
            return ((HB << 8) | LB);
        }

        // Обработка нажатия кнопки "Применить фильтр".
        private void button1_Click(object sender, EventArgs e)
        {
            if (_connection.State == ConnectionState.Open)
            {
                string s_date_1 = "'" + dateTimePicker1.Text + "'";
                string s_date_2 = "'" + dateTimePicker3.Text + "'";
                string s_time_1 = "'" + dateTimePicker2.Text + "'";
                string s_time_2 = "'" + dateTimePicker4.Text + "'";

                string search = _select + " WHERE ";

                if ((checkBox1.Checked == true) || (checkBox2.Checked == true))
                {
                    if ((checkBox1.Checked == true)) { search = search + $"(date BETWEEN '{dateTimePicker1.Value.Date.ToString("yyyy-MM-dd")}' AND '{dateTimePicker3.Value.Date.ToString("yyyy-MM-dd")}')"; }
                    if ((checkBox1.Checked == true) && (checkBox2.Checked == true)) { search = search + " AND "; }
                    if ((checkBox2.Checked == true)) { search = search + $"(time BETWEEN '{dateTimePicker2.Value.TimeOfDay.ToString()}' AND '{dateTimePicker4.Value.TimeOfDay.ToString()}')"; }
                    search += ";";

                    adapter_filter = new MySqlDataAdapter(search, _connection);
                    _table.Clear();
                    adapter_filter.Fill(_table);
                    dataGridView1.DataSource = _table;
                }
                else { MessageBox.Show("Поставьте галочки напротив фильтров, которые необходимо применить!"); }
            }
            else
            {
                timer1.Dispose();
                label10.Text = "отключена";
                MessageBox.Show("Подключение к базе данных прервано!" +
                    "Перезапустите программу!");
            }
        }

        // Выключаем таймер, закрываем соединение с БД
        // после закрытия приложения.
        private void OnApplicationExit(Object sender, EventArgs e)
        {
            timer1.Dispose();
            _connection.Close();
        }
    
        byte[] ReadExact(Stream s, int nbytes)
        {
            var buf = new byte[nbytes];
            var readpos = 0;
            while (readpos < nbytes)
            {
                readpos += s.Read(buf, readpos, nbytes - readpos);
            }
            return buf;
        }

        public static byte[] ModbusCRC16Calc(byte[] Message)
        {
            //выдаваемый массив CRC
            byte[] CRC = new byte[2];
            ushort Register = 0xFFFF; // создаем регистр, в котором будем сохранять высчитанный CRC
            ushort Polynom = 0xA001; //Указываем полином, он может быть как 0xA001(старший бит справа), так и его реверс 0x8005(старший бит слева, здесь не рассматривается), при сдвиге вправо используется 0xA001

            for (int i = 0; i < Message.Length; i++) // для каждого байта в принятом\отправляемом сообщении проводим следующие операции(байты сообщения без принятого CRC)
            {
                Register = (ushort)(Register ^ Message[i]); // Делим через XOR регистр на выбранный байт сообщения(от младшего к старшему)

                for (int j = 0; j < 8; j++) // для каждого бита в выбранном байте делим полученный регистр на полином
                {
                    if ((ushort)(Register & 0x01) == 1) //если старший бит равен 1 то
                    {
                        Register = (ushort)(Register >> 1); //сдвигаем на один бит вправо
                        Register = (ushort)(Register ^ Polynom); //делим регистр на полином по XOR
                    }
                    else //если старший бит равен 0 то
                    {
                        Register = (ushort)(Register >> 1); // сдвигаем регистр вправо
                    }
                }
            }

            CRC[1] = (byte)(Register >> 8); // присваеваем старший байт полученного регистра младшему байту результата CRC (CRClow)
            CRC[0] = (byte)(Register & 0x00FF); // присваеваем младший байт полученного регистра старшему байту результата CRC (CRCHi) это условность Modbus — обмен байтов местами.

            return CRC;
        }
    }
}
