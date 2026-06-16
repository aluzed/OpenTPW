using OpenTPW.UI;
using Veldrid;

namespace OpenTPW;

internal class PurpleButton : Panel
{
	// Shared across every button so we only build the atlas/texture once. GAME12 is used (not the
	// menu fonts) because it is a 1bpp font the decoder reads cleanly; MENU*/*AA are antialiased
	// (multi-bit) and that format isn't decoded yet, so they render as noise.
	private static Font? labelFont;
	private static Font LabelFont => labelFont ??= new Font( "Language/English/GAME12.bf4" );

	// How big the label is drawn relative to its native glyph size.
	private const float LabelScale = 2f;

	private Texture ButtonText1;
	private Texture ButtonText2;

	public string Text { get; set; }

	public PurpleButton( string text )
	{
		ButtonText1 = new Texture( "ui/textures/button_text1.wct", TextureFlags.PointFilter );
		ButtonText2 = new Texture( "ui/textures/button_text2.wct", TextureFlags.PointFilter );

		Text = Localization.Parse( text );
		Log.Info( $"Parsed '{text}' as '{Text}'" );
	}

	protected override void OnRender()
	{
		var material = Material.UI;
		var size = new Vector2( 200, 100 );
		var rect = new Rectangle( position, size );

		var uvs = new Rectangle( 0f, 0f, 1.0f, 0.5f );
		rect.Y -= 130;
		rect.X -= 740;

		//
		// End
		//
		material.Set( "Color", ButtonText1 );
		rect.X += 190;
		Graphics.Quad( rect, uvs.Shift( new Vector2( 0, 0.50f ) ), material );

		//
		// Middle
		//
		material.Set( "Color", ButtonText1 );
		rect.X += 190;
		Graphics.Quad( rect.Shift( new Vector2( 0, 28 ) ), uvs.Shift( new Vector2( 0, 0f ) ), material );

		//
		// Start
		//
		rect.X += 200;
		material.Set( "Color", ButtonText2 );
		Graphics.Quad( rect, uvs.Shift( new Vector2( 0f, 0.50f ) ), material );

		//
		// Label — centred on the button face. The three quads above span roughly
		// [position.X - 550, position.X + 40] horizontally; their centre is at
		// position.X - 255, and the face sits vertically around position.Y - 80.
		//
		if ( !string.IsNullOrEmpty( Text ) )
		{
			var centerX = position.X - 255;
			var centerY = position.Y - 80;
			// Centre on the label's actual ink (caps don't fill the line box, so centring the box
			// leaves text riding high). DrawText's originY is the line top; place it so the ink
			// midpoint lands on centerY: the ink sits (Top+Bottom)/2 * scale below the line top.
			var (inkTop, inkBottom) = LabelFont.Atlas.InkBounds( Text );
			var top = centerY + (inkTop + inkBottom) / 2f * LabelScale;
			Graphics.DrawText( LabelFont, Text, centerX, top, TextAlign.Center, LabelScale );
		}
	}
}
