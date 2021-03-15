﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.IO;

namespace Netdb
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        [Alias("p")]
        [Summary("Shows the bots ping")]
        public async Task Ping()
        {
            await ReplyAsync("Pong! 🏓 **" + Program._client.Latency + "ms**");
        }

        [Command("search")]
        [Alias("s")]
        [Summary("Shows you your searched movie")]
        public async Task Search([Remainder]string search)
        {
            if (!Tools.IsAvailableId(search))
            {
                Tools.Embedbuilder("This movie/series is not available", Color.DarkRed, Context.Channel);
                return;
            }

            Tools.Search(search,out EmbedBuilder eb,out FileStream stream,out string name);

            eb.WithTitle($"**{name.ToUpper()}**");

            await Context.Channel.SendFileAsync(stream, "example.png", embed: eb.Build());

            stream.Close();
        }

        [Command("rate")]
        [Alias("r")]
        [Summary("With this command you can rate movies/series")]
        public async Task RateMovie(int points, [Remainder] string moviename)
        {
            if (points < 1 || points > 10)
            {
                Tools.Embedbuilder("Your review has to be betweeen 1 and 10",Color.DarkRed, Context.Channel);
                return;
            }

            int id;

            if (!Tools.IsAvailable(moviename))
            {
                var cmd = Program._con.CreateCommand();
                cmd.CommandText = "select * from moviedata where id = '" + moviename + "';";
                var reader = await cmd.ExecuteReaderAsync();

                if (!reader.Read())
                {
                    Tools.Embedbuilder("This movie is not available", Color.DarkRed, Context.Channel);
                    reader.Close();
                    return;
                }

                reader.Close();
                id = Convert.ToInt32(moviename);
            }
            else
            {
                id = Tools.Getid(moviename);
            }

            if (Tools.Reader($"select * from reviewsdata where movieid = '{id}' and userid = '{Context.User.Id}';"))
            {
                Tools.Embedbuilder("You have already rated this movie", Color.DarkRed, Context.Channel);
                return;
            }
            else
            {
                Tools.RunCommand($"insert into reviewsdata (userid, movieid) values ('{Context.User.Id}', '{id}');");

                var cmd = Program._con.CreateCommand();
                cmd.CommandText = $"select * from moviedata where id = '{id}';";
                var reader = await cmd.ExecuteReaderAsync();

                reader.Read();

                int reviews = (int)reader["reviews"] + 1;
                int reviewpoints = (int)reader["reviewpoints"] + points;

                reader.Close();

                Tools.RunCommand($"update moviedata set reviews = '{reviews}' where id = '{id}';");
                Tools.RunCommand($"update moviedata set reviewpoints = '{reviewpoints}' where id = '{id}';");

                Tools.Embedbuilder("Thanks for your review", Color.Green, Context.Channel);
            }
        }

        [Command("recommend")]
        [Alias("rec")]
        [Summary("Recommend a movie to a certain user")]
        public async Task Recommend(IUser user, [Remainder]string movie)
        {
            if (!Tools.IsAvailableId(movie))
            {
                Tools.Embedbuilder("This movie/series is not available",Color.DarkRed,Context.Channel);
                return;
            }

            if (user.IsBot)
            {
                Tools.Embedbuilder("You can't send recommondation to other bots",Color.DarkRed,Context.Channel);
                return;
            }

            Tools.Search(movie,out EmbedBuilder eb, out FileStream stream,out string name);

            eb.WithAuthor(Context.User);
            eb.WithTitle("Recommended:\n**" + name.ToUpper() +"**");

            try
            {
                await user.SendFileAsync(stream, "example.png", embed: eb.Build());
            }
            catch (Exception)
            {
                Tools.Embedbuilder("Can't send message to this user", Color.DarkRed, Context.Channel);
            }

            stream.Close();
        }

        [Command("watchlist")]
        [Alias("w")]
        [Summary("Add or remove movies to your watchlist")]
        public async Task Watchlist(string input, [Remainder] string moviename = null)
        {
            if (input == "clear" || input == "c")
            {
                Tools.RunCommand($"delete from userdata where userid = '{Context.User.Id}';");

                await Context.Message.AddReactionAsync(new Emoji("✅"));
                return;
            }
            else if (input == "list" || input == "l")
            {
                int[] movieids = new int[27];
                int count = 0;

                var cmd = Program._con.CreateCommand();
                cmd.CommandText = $"select * from userdata where userid = '{Context.User.Id}';";
                var reader = cmd.ExecuteReader();

                EmbedBuilder eb = new EmbedBuilder();
                eb.WithColor(Color.DarkBlue);
                eb.WithAuthor(Context.User);
                eb.WithTitle("Watchlist");

                while (reader.Read())
                {
                    movieids[count] = (int)reader["movieid"];
                    count++;
                }

                reader.Close();

                for (int i = 0; i < count; i++)
                {
                    cmd = Program._con.CreateCommand();
                    cmd.CommandText = $"select * from moviedata where id = '{movieids[i]} ';";
                    reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        string link = "https://www.netflix.com/search?q=" + reader["moviename"];
                        link = link.Replace(" ", "");

                        if (Convert.ToBoolean(reader["type"]))
                        {
                            eb.AddField((string)reader["moviename"], " watch the series [here](" + link + ")");
                        }
                        else
                        {
                            eb.AddField((string)reader["moviename"], " watch the movie [here](" + link + ")");
                        }
                    }

                    reader.Close();
                }

                if (count == 0)
                {
                    eb.WithDescription("There are currently no movies or series in your warchlist");
                }

                await Context.Channel.SendMessageAsync("", false, eb.Build());
                return;
            }

            if (Tools.IsAvailable(moviename))
            {
                if (input == "add" || input == "a")
                {
                    if (Tools.Exists(Tools.Getid(moviename), Context.User.Id))
                    {
                        Tools.Embedbuilder("This movie is already in your watchlist", Color.DarkRed, Context.Channel);
                        return;
                    }

                    Tools.RunCommand($"insert into userdata (movieid, userid) values ('{Tools.Getid(moviename)}', '{Context.User.Id}');");

                    Tools.Embedbuilder("Added movie to your watchlist", Color.Green, Context.Channel);
                    return;
                }
                else if (input == "remove" || input == "r")
                {
                    if (Tools.Exists(Tools.Getid(moviename), Context.User.Id))
                    {
                        Tools.RunCommand($"delete from userdata where userid = '{Context.User.Id}' and movieid = '{Tools.Getid(moviename)}';");
                        await Context.Message.AddReactionAsync(new Emoji("✅"));
                        return;
                    }
                    else
                    {
                        Tools.Embedbuilder("This movie/series is not in your watchlist", Color.DarkRed, Context.Channel);
                        return;
                    }
                }
            }

            Tools.Embedbuilder("Couldn't find the movie", Color.DarkRed, Context.Channel);
        }

        [Command("top")]
        [Alias("t")]
        [Summary("Shows the searched list")]
        public async Task Top(int i)
        {
            EmbedBuilder eb = new EmbedBuilder();

            switch (i)
            {
                case 0:
                    eb.WithTitle("Top Reviewed");

                    var cmd = Program._con.CreateCommand();
                    cmd.CommandText = $"select * from bestreviewed;";
                    var reader = await cmd.ExecuteReaderAsync();

                    string movies = "";
                    string series = "";
                    int counter = 1;
                    while (reader.Read())
                    {
                        if (counter <= 25)
                        {
                            movies += counter + ". " + reader["name"] + " \n";
                        }
                        else if (counter > 50 && counter <= 75)
                        {
                            series += (counter - 50) + ". " + reader["name"] + " \n";
                        }

                        counter++;
                    }

                    reader.Close();

                    eb.AddField("Movies", movies);
                    eb.AddField("Series", series);

                    await Context.Channel.SendMessageAsync("", false, eb.Build());
                    break;
                case 1:
                    eb.WithTitle("Top Searched");

                    cmd = Program._con.CreateCommand();
                    cmd.CommandText = $"select * from mostsearched;";
                    reader = await cmd.ExecuteReaderAsync();

                    movies = "";
                    series = "";
                    counter = 1;
                    while (reader.Read())
                    {
                        if (counter <= 25)
                        {
                            movies += counter + ". " + reader["name"] + " \n";
                        }
                        else if (counter > 50 && counter <= 75)
                        {
                            series += (counter - 50) + ". " + reader["name"] + " \n";
                        }

                        counter++;
                    }

                    reader.Close();

                    eb.AddField("Movies", movies);
                    eb.AddField("Series", series);

                    await Context.Channel.SendMessageAsync("", false, eb.Build());
                    break;
                default:
                    break;
            }

            

           
        }

        [Command("subscribe")]
        [Alias("sc")]
        [Summary("When you are subscribed the bot will send you the newest movies daily")]
        public async Task Subscribe()
        {
            if (Context.Channel.GetType() == typeof(SocketDMChannel))
            {
                if (Tools.Reader($"select * from subscriberlist where guildid = '{0}' and channelid = '{Context.User.Id}';"))
                {
                    Tools.Embedbuilder("You have already subscribed",Color.DarkRed, Context.Channel);
                    return;
                }

                Tools.RunCommand($"insert into subscriberlist (channelid, since,guildid) values ('{Context.User.Id}', '{DateTime.Now.Date:yyyy-MM-dd}','{0}');");
            }
            else
            {
                IGuildUser user = (IGuildUser)Context.User;

                if (!user.GuildPermissions.Administrator)
                {
                    Tools.Embedbuilder("Only the server administators can use this command",Color.DarkRed, Context.Channel);
                    return;
                }

                var cmd = Program._con.CreateCommand();
                cmd.CommandText = $"select * from subscriberlist where guildid = '{Context.Guild.Id}';";
                var reader = await cmd.ExecuteReaderAsync();

                if (reader.Read())
                {
                    IGuildChannel channel = Program._client.GetGuild((ulong)(long)reader["guildid"]).GetChannel((ulong)(long)reader["channelid"]);

                    reader.Close();

                    Tools.Embedbuilder("This server already subscribed! See whats coming to Netflix in  " + MentionUtils.MentionChannel(channel.Id), Color.DarkRed, Context.Channel);
                    return;
                }
                reader.Close();

                Tools.RunCommand($"insert into subscriberlist (channelid, since,guildid) values ('{Context.Channel.Id}', '{DateTime.Now.Date:yyyy-MM-dd}','{Context.Guild.Id}');");
            }
            Tools.Embedbuilder("You will now recieve an update about whats coming to Netflix every day", Color.Green, Context.Channel);
        }

        [Command("unsubscribe")]
        [Alias("usc")]
        [Summary("Meh")]
        public async Task Unsubscribe()
        {
            ulong id;

            if (Context.Channel.GetType() != typeof(SocketDMChannel))
            {
                id = Context.User.Id;
            }
            else
            {
                IGuildUser user = (IGuildUser)Context.User;

                if (!user.GuildPermissions.Administrator)
                {
                    Tools.Embedbuilder("Only the server administators can use this command", Color.DarkRed, Context.Channel);
                    return;
                }

                id = Context.Channel.Id;
            }

            if (!Tools.Reader($"select * from subscriberlist where channelid = '{Context.Channel.Id}';"))
            {
                Tools.Embedbuilder("You are not subscribed", Color.DarkRed, Context.Channel);
                return;
            }
            
            Tools.RunCommand($"delete from subscriberlist where channelid = '{id}';");
            Tools.Embedbuilder("Ok :(",Color.Green, Context.Channel);
        }

        [Command("soon")]
        [Alias("so")]
        [Summary("Shows what's coming soon to Netflix")]
        public async Task Whatsnext()
        {
            EmbedBuilder eb = new EmbedBuilder();
            eb.WithColor(Color.LightOrange);
            eb.WithTitle("What's next?");

            bool content = false;
            int datesadded = 0;
            int counter = 0;

            do
            {
                DateTime releasedate = DateTime.Now.AddDays(counter);

                var cmd = Program._con.CreateCommand();
                cmd.CommandText = $"select * from comingsoon where releasedate = '{releasedate.Date:yyyy-MM-dd}';";
                var reader = await cmd.ExecuteReaderAsync();

                string movies = "";

                while (reader.Read())
                {
                    movies += reader["moviename"] + "\n";
                    content = true;
                }

                if (!reader.IsClosed)
                {
                    reader.Close();
                }

                if (movies != "")
                {
                    eb.AddField(releasedate.Date.ToString("MMMM dd"), movies);
                    datesadded++;
                }

                counter++;
            }
            while (datesadded < 5 && counter < 20);

            if (!content)
            {
                eb.WithDescription("There is nothing coming soon");
            }
            else
            {
                eb.WithDescription("Subscribe with `" + PrefixManager.GetPrefixFromGuildId(Context.Guild.Id) + "sc` to get a daily notification about what's new");
            }

            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }

        [Command("info")]
        [Alias("i")]
        [Summary("Gives information about the bot")]
        public async Task Info()
        {
            int movies = 0;
            int series = 0;
            int reviews = 0;

                var cmd = Program._con.CreateCommand();
                cmd.CommandText = $"select * from moviedata where type = '{0}';";
                var reader = await cmd.ExecuteReaderAsync();

                while (reader.Read())
                {
                    movies++;
                }
                reader.Close();

            cmd = Program._con.CreateCommand();
            cmd.CommandText = $"select * from moviedata where type = '{1}';";
            reader = await cmd.ExecuteReaderAsync();

            while (reader.Read())
            {
                series++;
            }
            reader.Close();

            cmd = Program._con.CreateCommand();
            cmd.CommandText = $"select * from reviewsdata;";
            reader = await cmd.ExecuteReaderAsync();

            while (reader.Read())
            {
                reviews++;
            }
            reader.Close();

            var eb = new EmbedBuilder();

            eb.WithColor(Color.Gold);
            eb.WithTitle("Netdb");
            eb.WithDescription("Super Cooler Bot");
            eb.AddField("Server", Context.Client.Guilds.Count);
            eb.AddField("Ping", Context.Client.Latency);
            eb.AddField("Currently in the database","movies: `" + movies + "` \n series: `" + series + "` \n reviews: `" + reviews + "`");
            eb.AddField("Invite the Bot", "[here](https://discord.com/oauth2/authorize?client_id=802237562625196084&scope=bot&permissions=518208)");
            eb.WithFooter("made by Yannick Füreder");

            await Context.Channel.SendMessageAsync("", false, eb.Build());
        }

    }
}
