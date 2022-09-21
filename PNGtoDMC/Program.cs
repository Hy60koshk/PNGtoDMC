using System;
using System.Drawing;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Drawing.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PNGtoDMC {
	class Program {
		class DMCColor {
			public string code;
			public string color;
			public string name;
			public string Symbol;
			public Color Color;
			public Brush Brush;
			public bool IsMinor = false;
			public int Number = 0;
			public DMCColor Subst = null;
		}

		static string SymbolsRaw = "1	2	3	4	5	6	7	8	9	#	$	%"
				+ "A C   D E   G H   J K   M N   O"
				+ "P   R T   U W   X Y   Z	@	¢	£	¤	¥"
				+ "Œ Ǣ   Ǯ Ǳ   Ψ Ω   Љ Њ   Ћ Ў"
				+ "Б Д   Ж Й   П Ф   Ц Ш   Ы Ю"
				+ "Ѩ Ѭ   Ѯ Ѹ   Ѿ	۞	߷	आ इ   ᚡ	⍟"
				+ "⏸	⏹	⏺	⓭	⓮	⓯	⓱	⓲	⓴	▲	╋	╬	▞"
				+ "☀	☂	☃	☆	☎	☢	☣	☤	☥	☭	☮	☯"
				+ "☸	☿	♂	♃	♄	♅	♊	♋	♌	♍	♐	♑	♒"
				+ "♞	♟	♠	♣	♥	♦	♫	⚤	⚧	⚽"
				+ "✂	✈	⧱	⨌	⳦	⳧"
				+ "㉚	㉛	㉜	㉟	㊲	㊳	㊵	㊷	㊹"
				+ "㋎	㋏	㊍	㊎	⁂	ꖵ ꖸ   ꖺ ꖻ"
				+ "ꔛ ꔞ   ꕥ";
		static char[] Symbols;

		static async Task<int> Main(string[] args) {
			Regex whitespaceRegex = new Regex("\\s+");
			SymbolsRaw = whitespaceRegex.Replace(SymbolsRaw, "");
			Symbols = SymbolsRaw.ToCharArray();
			DMCColor[] colors = JsonConvert.DeserializeObject<DMCColor[]>(File.ReadAllText("colors.json", Encoding.UTF8));

			PrivateFontCollection fontCollection = new PrivateFontCollection();
			//fontCollection.AddFontFile("Arial");
			Font basicFnt = new Font("Arial", 17F, FontStyle.Regular, GraphicsUnit.Pixel, 204);
			Font smallFnt = new Font("Arial", 12F, FontStyle.Regular, GraphicsUnit.Pixel, 204);
			Bitmap original = new Bitmap("sample.png");
			Bitmap result = new Bitmap(original.Width * 30, original.Height * 30);
			Graphics g = Graphics.FromImage(result);
			g.TextRenderingHint = TextRenderingHint.AntiAlias;
			Pen pen = new Pen(Color.AntiqueWhite);

			List<DMCColor> usedDMCcolors = new List<DMCColor>();
			Dictionary<int, DMCColor> colorMap = new Dictionary<int, DMCColor>();
			DMCColor[,] colorResult = new DMCColor[original.Width, original.Height];

			int manualBitmapHeightExtension = 0;

			Console.WriteLine("Preparing palitre...");
			if (!await Task<bool>.Run(() => {
				foreach (DMCColor dmcc in colors) {
					dmcc.Color = ColorTranslator.FromHtml(dmcc.color);
					int max = Math.Max(Math.Max(dmcc.Color.R, dmcc.Color.G), dmcc.Color.B);
					int d = 255 - max;
					dmcc.Brush = new SolidBrush(Color.FromArgb(dmcc.Color.R + d, dmcc.Color.G + d, dmcc.Color.B + d));
				}
				for (int i = 0; i < original.Width; i++) {
					for (int j = 0; j < original.Height; j++) {
						Color pixc = original.GetPixel(i, j);
						int argb = pixc.ToArgb();
						if (!colorMap.ContainsKey(argb)) {
							int delta = 500;
							DMCColor closest = null;
							foreach (DMCColor dmcc in colors) {
								if (dmcc.Color.ToArgb() == argb) {
									closest = dmcc;
									break;
								}
								int cd = Math.Abs(dmcc.Color.R - pixc.R) + Math.Abs(dmcc.Color.G - pixc.G) + Math.Abs(dmcc.Color.B - pixc.B);
								if (cd < delta) {
									delta = cd;
									closest = dmcc;
								}
							}
							colorMap.Add(argb, closest);
							if (!usedDMCcolors.Contains(closest)) {
								usedDMCcolors.Add(closest);
							}
							colorResult[i, j] = closest;
						}
						else {
							colorResult[i, j] = colorMap[argb];
						}
						colorResult[i, j].Number++;
					}
				}
				int ic = 0;
				for (; ic < usedDMCcolors.Count && ic < Symbols.Length; ic++) {
					usedDMCcolors[ic].Symbol = Symbols[ic].ToString();
				}
				for (; ic < usedDMCcolors.Count; ic++) {
					usedDMCcolors[ic].Symbol = "*" + ic;
					usedDMCcolors[ic].IsMinor = true;
				}
				for (ic = 0; ic < usedDMCcolors.Count; ic++) {
					if (usedDMCcolors[ic].Number < 10) {
						DMCColor thisColor = usedDMCcolors[ic];
						int delta = 500;
						DMCColor closest = null;
						foreach (DMCColor dmcc in usedDMCcolors) {
							if (dmcc != thisColor) {
								int cd = Math.Abs(dmcc.Color.R - thisColor.Color.R) + Math.Abs(dmcc.Color.G - thisColor.Color.G) + Math.Abs(dmcc.Color.B - thisColor.Color.B);
								if (cd < delta) {
									delta = cd;
									closest = dmcc;
								}
							}
						}
						if (delta < 20) {
							thisColor.Subst = closest;
							manualBitmapHeightExtension++;
							//closest.Number += thisColor.Number;
							//thisColor.Symbol = closest.Symbol;
							//thisColor.IsMinor = closest.IsMinor;
						}
					}
				}
				return true;
			})) {
				return 1;
			}
			Console.WriteLine("Rendering new image...");
			if (!await Task<bool>.Run(() => {
				for (int i = 0; i < original.Width; i++) {
					for (int j = 0; j < original.Height; j++) {
						DMCColor dmcc = colorResult[i, j];
						g.DrawRectangle(Pens.DarkGray, i * 30, j * 30, 30, 30);
						g.FillRectangle(dmcc.Brush, i * 30 + 1, j * 30 + 1, 29, 29);
						g.DrawString(dmcc.Symbol, dmcc.IsMinor ? smallFnt : basicFnt, Brushes.Black, i * 30 + 3, j * 30 + 5);
					}
				}
				return true;
			})) {
				return 1;
			}
			g.Flush();
			result.Save("result.png");

			int resultCount = usedDMCcolors.Count + manualBitmapHeightExtension;
			int resultWidth = (int)Math.Ceiling((double)resultCount / 50);

			result = new Bitmap(resultWidth * 350, 32 * Math.Min(resultCount, 50));
			g = Graphics.FromImage(result);
			g.TextRenderingHint = TextRenderingHint.AntiAlias;

			Console.WriteLine("Generating manual...");
			StringBuilder sbManual = new StringBuilder();
			if (!await Task<bool>.Run(() => {
				int ivr = 0;
				int ihr = 0;
				Pen borderPen = Pens.DarkGray;
				foreach (DMCColor dmcc in usedDMCcolors) {
					if (ivr > 49) {
						ihr++;
						ivr = 0;
					}
					int offhr = ihr * 350;
					sbManual.Append(dmcc.code).Append(" : ").Append(dmcc.Number).Append("\r\n");

					g.FillRectangle(new SolidBrush(dmcc.Color), offhr + 150, ivr * 32, 70, 32);
					g.DrawString(dmcc.Symbol, basicFnt, Brushes.Black, offhr + 6, ivr * 32 + 7);
					g.DrawString(dmcc.code, basicFnt, Brushes.Black, offhr + 56, ivr * 32 + 7);
					g.DrawString(dmcc.Number.ToString(), basicFnt, Brushes.Black, offhr + 226, ivr * 32 + 7);
					g.DrawRectangle(borderPen, offhr + 0, ivr * 32, 50, 32);
					g.DrawRectangle(borderPen, offhr + 50, ivr * 32, 100, 32);
					g.DrawRectangle(borderPen, offhr + 150, ivr * 32, 70, 32);
					g.DrawRectangle(borderPen, offhr + 220, ivr * 32, 70, 32);
					ivr++;
					if (dmcc.Subst != null) {
						sbManual.Append(dmcc.code).Append(" <> ").Append(dmcc.Subst.code).Append(" ==> ").Append(dmcc.Subst.Number + dmcc.Number).Append("\r\n");

						g.FillRectangle(new SolidBrush(dmcc.Color), offhr + 75, ivr * 32, 70, 32);
						g.FillRectangle(new SolidBrush(dmcc.Subst.Color), offhr + 145, ivr * 32, 70, 32);
						g.DrawString(dmcc.code, basicFnt, Brushes.Black, offhr + 6, ivr * 32 + 7);
						g.DrawString(dmcc.Subst.code, basicFnt, Brushes.Black, offhr + 221, ivr * 32 + 7);
						g.DrawRectangle(borderPen, offhr + 0, ivr * 32, 75, 32);
						g.DrawRectangle(borderPen, offhr + 75, ivr * 32, 70, 32);
						g.DrawRectangle(borderPen, offhr + 145, ivr * 32, 70, 32);
						g.DrawRectangle(borderPen, offhr + 215, ivr * 32, 75, 32);
						ivr++;
					}
				}
				return true;
			})) {
				return 1;
			}
			g.Flush();
			result.Save("manual.png");
			File.WriteAllText("resultColors.txt", sbManual.ToString(), Encoding.UTF8);

			Console.WriteLine("Done!");
			Console.ReadKey();
			return 0;
		}
	}
}
