using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;

using System.Threading.Tasks;

namespace PruebaTorneosAPI
{
    internal class Program
    {
        class PruebaTorneoApi
        {
           
            const string BASE_URL = "https://torneoapi.onrender.com";

            static HttpClient client = new HttpClient();
            static Random random = new Random();

            static async Task Main(string[] args)
            {
                client.BaseAddress = new Uri(BASE_URL);
                Console.Title = "Simulador de Torneos - Cliente Remoto";

                try
                {
                    Console.WriteLine($"Conectando a: {BASE_URL} ...\n");

                    
                    Console.WriteLine("--- PASO 1: CREAR TORNEO ---");
                    
                    var torneoId = await CrearTorneo();
                    if (torneoId == 0) return;

                    Console.WriteLine("\n--- PASO 2: INSCRIBIR 16 EQUIPOS ---");
                
                    await InscribirEquipos(torneoId);

                   
                    Console.WriteLine("\n--- PASO 3: INICIAR TORNEO (Generar Grupos) ---");
                  
                    if (!await EjecutarAccion($"/api/Torneo/{torneoId}/iniciar")) return;


                    Console.WriteLine("\n--- PASO 4: JUGANDO FASE DE GRUPOS ---");
                    await SimularPartidos(torneoId, 1); 

                    Console.WriteLine("\n--- PASO 5: AVANZANDO DE RONDA ---");
                    if (!await EjecutarAccion($"/api/Torneo/{torneoId}/avanzar")) return;
                    Console.WriteLine("\n--- PASO 6: FINALIZANDO TORNEO ---");
                    await SimularPartidos(torneoId, 3); 
                    await EjecutarAccion($"/api/Torneo/{torneoId}/avanzar");

                    await SimularPartidos(torneoId, 4); 
                    await EjecutarAccion($"/api/Torneo/{torneoId}/avanzar");

                    await SimularPartidos(torneoId, 5);
                    await EjecutarAccion($"/api/Torneo/{torneoId}/avanzar");

                    Console.WriteLine("\n--- PASO 7: CONSULTAR CAMPEÓN ---");
                    await MostrarCampeon(torneoId);

                    Console.WriteLine("\n✅ SIMULACIÓN COMPLETADA");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n❌ ERROR: {ex.Message}");
                }

                Console.WriteLine("Presiona Enter para salir...");
                Console.ReadLine();
            }

            static async Task<int> CrearTorneo()
            {
                var torneo = new
                {
                    nombre = "Copa Consola 2024",
                    fechaInicio = DateTime.UtcNow,
                    fechaFin = DateTime.UtcNow.AddMonths(1),
                    tipo = 3,
                    estado = 1,
                    minEquipos = 8,
                    maxEquipos = 32
                };

                Console.WriteLine("Enviando JSON...");
                string jsonString = JsonConvert.SerializeObject(torneo);
                Console.WriteLine(jsonString);

                var response = await PostJson("/api/Torneo", torneo); 

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Respuesta: {content}");
                    dynamic resultado = JsonConvert.DeserializeObject(content);
                    return resultado.id ?? 0;
                }

                Console.WriteLine($"❌ Error al crear torneo: {response.StatusCode}");
                string errorDetalle = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️ DETALLE DEL ERROR: {errorDetalle}");

                return 0;
            }

            static async Task InscribirEquipos(int torneoId)
            {
                for (int i = 1; i <= 16; i++)
                {
                    var equipo = new { Nombre = $"Equipo {i} FC", Entrenador = "Bot DT" };
                    var resEq = await PostJson("/api/Equipos", equipo);
                    var contentEq = await resEq.Content.ReadAsStringAsync();
                    dynamic eqObj = JsonConvert.DeserializeObject(contentEq);
                    int equipoId = eqObj.id ?? eqObj.data?.id ?? 0;
                    var jugador = new { Nombre = $"Goleador {i}", NumeroCamiseta = 10, EquipoId = equipoId };
                    await PostJson("/api/Jugador", jugador);

                    var inscripcion = new { TorneoId = torneoId, EquipoId = equipoId };
                    await PostJson("/api/Inscripciones", inscripcion);

                    Console.Write($"\rInscritos: {i}/16...   ");
                }
                Console.WriteLine("\n✅ Equipos inscritos.");
            }

            static async Task<bool> EjecutarAccion(string endpoint)
            {
                var response = await client.PostAsync(endpoint, null);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Acción OK: " + endpoint);
                    return true;
                }
                string error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"⚠️ Error en API ({endpoint}): {response.StatusCode}");
                Console.WriteLine($"   Detalle: {error}");
                return false;
            }

            static async Task SimularPartidos(int torneoId, int fase)
            {
                var res = await client.GetAsync($"/api/Partidos?torneoId={torneoId}&fase={fase}&jugado=false");
                if (!res.IsSuccessStatusCode) return;

                var json = await res.Content.ReadAsStringAsync();
                List<PartidoDto> partidos = JsonConvert.DeserializeObject<List<PartidoDto>>(json);

                if (partidos == null || partidos.Count == 0) return;

                foreach (var p in partidos)
                {
                    int golesL = random.Next(0, 5);
                    int golesV = random.Next(0, 5);
                    if (fase > 1 && golesL == golesV) golesL++;


                    p.GolesLocal = golesL;
                    p.GolesVisitante = golesV;
                    p.Jugado = true;

                    await PutJson($"/api/Partidos/{p.Id}", p);
                }
                Console.WriteLine($"   ⚽ Se simularon {partidos.Count} partidos.");
            }

            static async Task MostrarCampeon(int torneoId)
            {
              
                var res = await client.GetAsync($"/api/Torneo/{torneoId}");
                var json = await res.Content.ReadAsStringAsync();
                dynamic torneo = JsonConvert.DeserializeObject(json);

           
                int estado = torneo.estado ?? torneo.data?.estado ?? 0;

                Console.WriteLine("\n=============================================");
                if (estado == 3) 
                {
               
                    var resPartidos = await client.GetAsync($"/api/Partidos?torneoId={torneoId}&fase=5&jugado=true");
                    var jsonPartidos = await resPartidos.Content.ReadAsStringAsync();
                    List<PartidoDto> finales = JsonConvert.DeserializeObject<List<PartidoDto>>(jsonPartidos);

                    var final = finales.FirstOrDefault();
                    if (final != null)
                    {
                        int idCampeon = (final.GolesLocal > final.GolesVisitante) ? final.EquipoLocalId : final.EquipoVisitanteId;

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("       🏆 ¡TENEMOS UN CAMPEÓN! 🏆       ");
                        Console.WriteLine("=============================================");
                        Console.WriteLine($"\n      🎉  EL EQUIPO ID {idCampeon} HA GANADO LA COPA  🎉\n");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.WriteLine("⚠️ El torneo aún no ha finalizado.");
                }
                Console.WriteLine("=============================================");
            }   

            static async Task<HttpResponseMessage> PostJson(string uri, object data)
            {
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                return await client.PostAsync(uri, content);
            }

            static async Task<HttpResponseMessage> PutJson(string uri, object data)
            {
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                return await client.PutAsync(uri, content);
            }
        }

        public class PartidoDto
        {
            public int Id { get; set; }
            public int TorneoId { get; set; }
            public int EquipoLocalId { get; set; }
            public int EquipoVisitanteId { get; set; }
            public int GolesLocal { get; set; }
            public int GolesVisitante { get; set; }
            public bool Jugado { get; set; }
            public int Fase { get; set; }
        }

    }
}
