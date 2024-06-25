// ************************************************************************
// Practica 07
// David Chicaiza
// Fecha de realización: 21/06/2024
// Fecha de entrega: 26/06/2024
// Resultados:
// - Se corrigio el error detectado en la clase en el lado del cliente que correspondia  a las lineas de Finally en el cierre de las conexiones, las lineas afectadas son:
// FrmValidador_Load

//Estas lineas causan error ya que hace que se cierren las conexiones al momento en el que el form se cargue
//se las quita para que la ejecucion del programa continue.
//finally 
//{
//    flujo?.Close();
//    remoto?.Close();
//}
// HazOperacion

//Estas lineas se eliminan ya que cierran la conexion causando error igual que en el FrmValidador_Load
//finally 
//{
//    flujo?.Close();
//    remoto?.Close();
//}
//
// -Se implemento la clase protocolo para que maneje las acciones que comunicaba al cliente y servidor, de este modo se logra un orden adecuado en la construccion de la aplicacion
// ya que se tendra capas y niveles bien definidos.
// Conclusiones:
// La creación de la clase Protocolo permitió centralizar la lógica de procesamiento de los pedidos y las respuestas, lo que resultó en una mejor modularización del código.
// Esto facilita la reutilización y el mantenimiento del código, ya que cualquier cambio en la lógica del protocolo solo necesita ser realizado en un único lugar, en lugar
// de en múltiples puntos del código del cliente y del servidor.
//
// Se demostró la importancia de manejar correctamente las conexiones de red en aplicaciones cliente-servidor. La eliminación de cierres prematuros del flujo y del cliente
// en el lado del cliente permitió que la comunicación se mantuviera abierta durante toda la sesión, evitando excepciones de tipo ObjectDisposedException.
//
// Recomendaciones:
// Es recomendable mejorar el manejo de errores tanto en el cliente como en el servidor. Actualmente, los mensajes de error se muestran al usuario final de una manera genérica.
// Implementar un sistema de logging que registre estos errores detalladamente y proporcionar mensajes de error más amigables y específicos al usuario final puede mejorar la
// experiencia del usuario y facilitar la resolución de problemas.
//
// 
//

using System;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Protocolo
{
    // Clase que representa un Pedido con un comando y sus parámetros
    public class Pedido
    {
        public string Comando { get; set; }
        public string[] Parametros { get; set; }

        // Método estático que procesa un mensaje recibido y crea un objeto Pedido
        public static Pedido Procesar(string mensaje)
        {
            var partes = mensaje.Split(' ');
            return new Pedido
            {
                Comando = partes[0],
                Parametros = partes.Skip(1).ToArray()
            };
        }
    }

    // Clase que representa una Respuesta con estado y mensaje
    public class Respuesta
    {
        public string Estado { get; set; }
        public string Mensaje { get; set; }

        public override string ToString()
        {
            return $"{Estado} {Mensaje}";
        }
    }

    // Clase Protocolo que maneja las operaciones y resolución de pedidos
    public class Protocolo
    {
        // Método que realiza una operación enviando un pedido a través de un flujo de red
        public static Respuesta HazOperacion(Pedido pedido, NetworkStream flujo)
        {
            if (flujo == null)
            {
                throw new InvalidOperationException("No hay conexión");
            }

            try
            {
                // Serializar el pedido a un arreglo de bytes
                byte[] bufferTx = Encoding.UTF8.GetBytes(
                    pedido.Comando + " " + string.Join(" ", pedido.Parametros));

                // Enviar el pedido a través del flujo
                flujo.Write(bufferTx, 0, bufferTx.Length);

                // Buffer para recibir la respuesta
                byte[] bufferRx = new byte[1024];

                // Leer la respuesta del flujo
                int bytesRx = flujo.Read(bufferRx, 0, bufferRx.Length);

                // Convertir la respuesta de bytes a string
                string mensaje = Encoding.UTF8.GetString(bufferRx, 0, bytesRx);

                // Procesar la respuesta y crear un objeto Respuesta
                var partes = mensaje.Split(' ');

                return new Respuesta
                {
                    Estado = partes[0],
                    Mensaje = string.Join(" ", partes.Skip(1).ToArray())
                };
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException("Error al intentar transmitir: " + ex.Message);
            }
        }

        // Método que resuelve un pedido recibido en el servidor
        public static Respuesta ResolverPedido(Pedido pedido, string direccionCliente, Dictionary<string, int> listadoClientes)
        {
            Respuesta respuesta = new Respuesta
            { Estado = "NOK", Mensaje = "Comando no reconocido" };

            // Evaluar el comando del pedido
            switch (pedido.Comando)
            {
                case "INGRESO":
                    // Comando de ingreso con validación de credenciales
                    if (pedido.Parametros.Length == 2 &&
                        pedido.Parametros[0] == "root" &&
                        pedido.Parametros[1] == "admin20")
                    {
                        respuesta = new Random().Next(2) == 0
                            ? new Respuesta
                            {
                                Estado = "OK",
                                Mensaje = "ACCESO_CONCEDIDO"
                            }
                            : new Respuesta
                            {
                                Estado = "NOK",
                                Mensaje = "ACCESO_NEGADO"
                            };
                    }
                    else
                    {
                        respuesta.Mensaje = "ACCESO_NEGADO";
                    }
                    break;

                case "CALCULO":
                    // Comando de cálculo con validación de placa
                    if (pedido.Parametros.Length == 3)
                    {
                        string modelo = pedido.Parametros[0];
                        string marca = pedido.Parametros[1];
                        string placa = pedido.Parametros[2];
                        if (ValidarPlaca(placa))
                        {
                            byte indicadorDia = ObtenerIndicadorDia(placa);
                            respuesta = new Respuesta
                            {
                                Estado = "OK",
                                Mensaje = $"{placa} {indicadorDia}"
                            };
                            ContadorCliente(direccionCliente, listadoClientes);
                        }
                        else
                        {
                            respuesta.Mensaje = "Placa no válida";
                        }
                    }
                    break;

                case "CONTADOR":
                    // Comando para obtener el contador de solicitudes de un cliente
                    if (listadoClientes.ContainsKey(direccionCliente))
                    {
                        respuesta = new Respuesta
                        {
                            Estado = "OK",
                            Mensaje = listadoClientes[direccionCliente].ToString()
                        };
                    }
                    else
                    {
                        respuesta.Mensaje = "No hay solicitudes previas";
                    }
                    break;
            }

            return respuesta;
        }

        // Validar la placa con una expresión regular
        private static bool ValidarPlaca(string placa)
        {
            return Regex.IsMatch(placa, @"^[A-Z]{3}[0-9]{4}$");
        }

        // Obtener el indicador de día según el último dígito de la placa
        private static byte ObtenerIndicadorDia(string placa)
        {
            int ultimoDigito = int.Parse(placa.Substring(6, 1));
            switch (ultimoDigito)
            {
                case 1:
                case 2:
                    // Lunes
                    return 0b00100000;
                case 3:
                case 4:
                    // Martes
                    return 0b00010000;
                case 5:
                case 6:
                    // Miércoles
                    return 0b00001000;
                case 7:
                case 8:
                    // Jueves
                    return 0b00000100;
                case 9:
                case 0:
                    // Viernes
                    return 0b00000010;
                default:
                    return 0;
            }
        }

        // Incrementar el contador de solicitudes para un cliente
        private static void ContadorCliente(string direccionCliente, Dictionary<string, int> listadoClientes)
        {
            if (listadoClientes.ContainsKey(direccionCliente))
            {
                listadoClientes[direccionCliente]++;
            }
            else
            {
                listadoClientes[direccionCliente] = 1;
            }
        }
    }
}


