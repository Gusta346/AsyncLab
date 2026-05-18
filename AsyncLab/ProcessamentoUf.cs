using System.Diagnostics;
using System.Text;
using System.Text.Json;

static class ProcessamentoUf
{
    public static async Task ProcessarUfAsync(string uf, List<Municipio> listaUf, string outRoot, int pbkdf2Iterations, int hashBytes)
    {
        // Ordena por Nome preferido para saída consistente
        listaUf.Sort((a, b) => string.Compare(a.NomePreferido, b.NomePreferido, StringComparison.OrdinalIgnoreCase));

        Console.WriteLine($"Processando UF: {uf} ({listaUf.Count} municípios)");
        var swUf = Stopwatch.StartNew();
        string outPath = Path.Combine(outRoot, $"municipios_hash_{uf}.csv");
        await using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        await using (var swOut = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            await swOut.WriteLineAsync("TOM;IBGE;NomeTOM;NomeIBGE;UF;Hash");

            var listaJson = new List<object>();
            int count = 0;
            foreach (var m in listaUf)
            {
                // Password: todos os campos concatenados; Salt: IBGE + “pepper” fixo (opcional)
                string password = m.ToConcatenatedString();
                byte[] salt = Util.BuildSalt(m.Ibge);

                // Trabalho pesado real (PBKDF2/SHA-256)
                string hashHex = Util.DeriveHashHex(password, salt, pbkdf2Iterations, hashBytes);

                await swOut.WriteLineAsync($"{m.Tom};{m.Ibge};{m.NomeTom};{m.NomeIbge};{m.Uf};{hashHex}");

                listaJson.Add(new {
                    m.Tom,
                    m.Ibge,
                    m.NomeTom,
                    m.NomeIbge,
                    m.Uf,
                    Hash = hashHex
                });

                count++;
                if (count % 50 == 0 || count == listaUf.Count)
                {
                    Console.WriteLine($"  Parcial: {count}/{listaUf.Count} municípios processados para UF {uf} | Tempo parcial: {FormatTempo(swUf.ElapsedMilliseconds)}");
                }
            }

            // Salva JSON
            string jsonPath = Path.Combine(outRoot, $"municipios_hash_{uf}.json");
            var json = JsonSerializer.Serialize(listaJson, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(jsonPath, json, Encoding.UTF8);
        }

        swUf.Stop();
        Console.WriteLine($"UF {uf} concluída. Arquivos gerados: CSV e JSON. Tempo total UF: {FormatTempo(swUf.ElapsedMilliseconds)}");
    }

    private static string FormatTempo(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return $"{ts.Minutes}m {ts.Seconds}s {ts.Milliseconds}ms";
    }
}
