﻿// =============================================================================
// MIT License
// 
// Copyright (c) 2018 Valeriya Pudova (hww.github.io)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// =============================================================================

using System;
using UnityEngine;

namespace Code.Display
{
    [CreateAssetMenu(menuName = "VARP/Display/Display", fileName = "display")]
    public class TextDisplay : ScriptableObject, IDisplay
    {
        // ----------------------------------------------------------------------------------------------------
        // -- Constructors 
        // ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// Create terminal with this geometry
        /// </summary>
        public TextDisplay ( int sizeX, int sizeY,  Xresources colorTheme = null)
        {
            // load resources 
            theme = colorTheme!=null ? colorTheme : TangoTheme.Create ( );
            cursor = new DisplayCursor ( this );
            backgroundMaterial = ReadMaterial ( "VARP/DebugDraw/GLlineZOff" );
            defaultMaterial = ReadMaterial("VARP/DebugDraw/GLFontZOff");
            defaultFont = ReadFont("VARP/DebugDraw/GLFont");
            // get font's information
            lineHeight = defaultFont.lineHeight;
            charAdvance = GetMaxCharWidth();
            SetBufferSize( sizeX, sizeY );
            ResetColor();
        }

        // for all ASCII characters check the advance field to chose biggest
        private float GetMaxCharWidth()
        {
            float max = 0;
            CharacterInfo space = new CharacterInfo();
            if (defaultFont.GetCharacterInfo((char) '█', out space))
            {
                max = Math.Max(max, space.advance);
            }
            else
            {
                for (var c = 0; c < 255; c++)
                {
                    if (defaultFont.GetCharacterInfo((char) c, out space))
                        max = Math.Max(max, space.advance);
                }
            }
            return max;
        }

        // ----------------------------------------------------------------------------------------------------
        // -- ITerminal Methods 
        // ----------------------------------------------------------------------------------------------------

        /// <inheritdoc />
        public void Write ( char c )
        {
            var idx = 0;
            WriteInternal( c, ref idx );
        }
        
        /// <inheritdoc />
        public void Write ( string message )
        {
            if ( message == null )
                throw new ArgumentNullException ();
            if (message.Length == 0)
                return;
            var idx = 0;
            WriteInternal ( message, ref idx );
        }

        /// <inheritdoc />
        public void WriteLine ( string message )
        {
            if ( message == null )
                throw new ArgumentNullException ();
            if ( message.Length == 0 )
                return;
            var idx = 0;
            WriteInternal ( message, ref idx );
            cursor.NewLine ( );
        }
        
        /// <inheritdoc />
        public void WriteFormat ( string format, params object[] args)
        {
            if ( format == null )
                throw new ArgumentNullException ();
            var idx = 0;
            WriteInternal ( string.Format(format, args), ref idx );
        }

        private void WriteInternal ( string message, ref int idx )
        {
            while ( idx < message.Length )
                WriteInternal( message[ idx ], ref idx );
        }
        private void WriteInternal(char c, ref int idx )
        {
            if ( c < ' ' )
            {
                // Escape codes 
                switch ( c )
                {
                    case '\a':
                        Beep ( );
                        idx++;
                        break;
                    case '\b':
                        cursor.AddX ( -1 );
                        WriteVisibleCharacter ( ' ', cursor.X, cursor.Y );
                        idx++;
                        break;
                    case '\t':
                        Tab ( );
                        idx++;
                        break;
                    case '\n':
                        cursor.NewLine ( );
                        idx++;
                        break;
                    case '\r':
                        cursor.X = cursor.WindowLeft;
                        idx++;
                        break;
                    case (char)27:
                        WriteEscapeCharacter ( c, ref idx );
                        break;
                    default:
                        idx++;
                        break;
                }
            }
            else if ( c == (char)127 )
            {
                for ( var x = cursor.X ; x < cursor.WindowRight - 1 ; x++ )
                {
                    var addr = x + cursor.Y * BufferWidth;
                    IsNegativeBuffer[ addr ] = IsNegativeBuffer[ addr + 1 ];
                    ForegroundBuffer[ addr ] = ForegroundBuffer[ addr + 1 ];
                    CharacterBuffer[ addr ] = CharacterBuffer[ addr + 1 ];
                }
                WriteVisibleCharacter ( ' ', cursor.X, cursor.Y );
                idx++;
            }
            else
            {
                WriteVisibleCharacter ( c, cursor.X, cursor.Y );
                cursor.AddX ( 1 );
                idx++;
            }
        }

        // TODO! Implement ESC sequences
        private void WriteEscapeCharacter ( char c, ref int idx )
        {
        }

        private void WriteVisibleCharacter ( char c, int x, int y )
        {
            IsNegativeBuffer[ x + y * BufferWidth ] = IsNegative;
            ForegroundBuffer[ x + y * BufferWidth ] = foregroundColor;
            CharacterBuffer[ x + y * BufferWidth ] = c;
        }

        /// <inheritdoc />
        public void Beep ( )
        {
            //if (beepClip!=null) beepSound.PlayOneShot(beepClip);
        }

        // ----------------------------------------------------------------------------------------------------
        // Buffer management
        // ----------------------------------------------------------------------------------------------------

        /// <inheritdoc />
        public void SetBufferSize ( int width, int height )
        {
            BufferWidth = width;
            BufferHeight = height;
            bufferSize = width * height * width;
            cursor.SetBufferSize ( width, height );
            cursor.SetWindowPosition( 0,0 );
            cursor.SetWindowSize ( width, height );
            // allocate memory
            charactersPositions = new Vector3[ bufferSize ];
            IsNegativeBuffer = new bool[ bufferSize ];
            CharacterBuffer = new char[ bufferSize ];
            ForegroundBuffer = new Color[ bufferSize ];
            negativeCharactersIndices = new int[ bufferSize ];
            // calculate background and the text size
            textWidth = charAdvance * BufferWidth;
            textHeight = lineHeight * BufferHeight;
            backgroundWidth = textWidth + backgroundPads.x * 2;
            backgroundHeight = textHeight + backgroundPads.y * 2;
            backgroundRectangle = new Rect ( 0, 0, backgroundWidth, backgroundHeight);
            // fill buffer coordinates
            textPosition = new Vector2(backgroundPads.x, backgroundPads.y);
            for (var y = 0; y < BufferHeight; y++)
            {
                for (var x = 0; x < BufferWidth; x++)
                    charactersPositions[x + y * BufferWidth] = GetCharacterPosition(x, y);
            }
            Clear( );  // clear screen
        }

        /// <inheritdoc />
        public int BufferWidth
        {
            get;
            private set; 
        }

        /// <inheritdoc />
        public int BufferHeight
        {
            get ; 
            private set;
        }

        // ----------------------------------------------------------------------------------------------------
        // -- Clear 
        // ----------------------------------------------------------------------------------------------------

        /// <inheritdoc />
        public void Clear ( )
        {
            ClearRectangle ( cursor.WindowLeft, cursor.WindowTop, cursor.WindowWidth, cursor.WindowHeight);
            cursor.SetPosition ( cursor.WindowLeft, cursor.WindowLeft );
        }

        private void ClearRectangle ( int targetLeft, int targetTop, int targetWidth, int targetHeight )
        {
            ClearRectangle ( targetLeft, targetTop, targetWidth, targetHeight, ' ', foregroundColor);
        }

        private void ClearRectangle ( int targetLeft, int targetTop, int targetWidth, int targetHeight, char character, Color color)
        {
            Debug.Assert ( targetLeft >= 0 && targetLeft < BufferWidth );
            Debug.Assert ( targetTop >= 0 && targetTop < BufferHeight );
            Debug.Assert ( targetWidth >= 0 && targetWidth <= ( BufferWidth - targetLeft ) );
            Debug.Assert ( targetHeight >= 0 && targetHeight <= ( BufferHeight - targetTop ) );
            var xmax = targetWidth - targetLeft - 1;
            var ymax = targetHeight - targetTop - 1;
            for ( var y = targetTop ; y <= ymax ; y++ )
            {
                for (var x = targetLeft; x <= xmax; x++)
                {
                    CharacterBuffer[ x + y * BufferWidth ] = character;
                    ForegroundBuffer[ x + y * BufferWidth ] = color;
                }
            }
        }

        // ----------------------------------------------------------------------------------------------------
        // Cursor Position
        // ----------------------------------------------------------------------------------------------------

        /// <inheritdoc />
        public void SetCursor ( int x, int y )
        {
            cursor.SetPosition ( x, y );
        }

        // Fill this columnt up to next tab
        private void Tab ( )
        {
            var xtab = cursor.GetCharsToEndOfTabColumn ( );
            for ( var i = 0 ; i <= xtab ; i++ )
            {
                WriteVisibleCharacter ( ' ', cursor.X, cursor.Y );
                cursor.AddX ( 1 );
            }
        }

        private Vector3 GetCursorSize ( )
        {
            return new Vector3 ( charAdvance, lineHeight, 0 );
        }

        private Vector3 GetCharacterPosition(int x, int y)
        {
            return new Vector3(textPosition.x + charAdvance * x, lineHeight * (BufferHeight-y-1) + textPosition.y /*- lineHeight*/, 0);
        }

        // ----------------------------------------------------------------------------------------------------
        // Window geometry
        // ----------------------------------------------------------------------------------------------------

        /// <inheritdoc />
        public void SetWindowPosition ( int x, int y )
        {
            cursor.SetWindowPosition ( x, y );
        }

        /// <inheritdoc />
        public void SetWindowSize ( int width, int height )
        {
            cursor.SetWindowSize ( width, height );
        }

        /// <inheritdoc />
        public int WindowLeft { 
            get => cursor.WindowLeft;
            set => cursor.WindowLeft = value;
        }

        /// <inheritdoc />
        public int WindowTop { 
            get => cursor.WindowLeft;
            set => cursor.WindowTop = value;
        }

        /// <inheritdoc />
        public int WindowWidth { 
            get => cursor.WindowLeft;
            set => cursor.WindowWidth = value;
        }

        /// <inheritdoc />
        public int WindowHeight { 
            get => cursor.WindowLeft;
            set => cursor.WindowHeight = value;
        }
        
        // ----------------------------------------------------------------------------------------------------
        // -- Coloring
        // ----------------------------------------------------------------------------------------------------

        /// <inheritdoc />
        public void ResetColor ( )
        {
            foregroundColor = theme.Foreground;
        }
        
        /// <inheritdoc />
        public void SetColor ( Color color )
        {
            foregroundColor = color;
        }
        
        /// <inheritdoc />
        public void SetBackgroundColor ( Color color )
        {
            theme.Background = color;
        }

        // ----------------------------------------------------------------------------------------------------
        // -- Scrolling 
        // ----------------------------------------------------------------------------------------------------
        
        /// <inheritdoc />
        public void MoveBufferArea ( int sourceLeft,
                                    int sourceTop,
                                    int sourceWidth,
                                    int sourceHeight,
                                    int targetLeft,
                                    int targetTop)
        {
            MoveBufferArea ( sourceLeft, sourceTop, sourceWidth, sourceHeight, targetLeft, targetTop, ' ', foregroundColor );
        }
        
        /// <inheritdoc />
        public void MoveBufferArea (int sourceLeft,
                                    int sourceTop,
                                    int sourceWidth,
                                    int sourceHeight,
                                    int targetLeft,
                                    int targetTop,
                                    char character,
                                    Color color)
        {
            var sourceBottom = sourceTop + sourceHeight;
            var targetBottom = targetTop + sourceHeight;
            if ( sourceTop >= targetTop )
            {
                var targetLine = targetTop;
                for ( var srcLine = sourceTop ; srcLine <= sourceBottom ; srcLine++, targetLine++ )
                    CopyLine ( sourceLeft, srcLine, targetLeft, targetLine, sourceWidth );
                while (targetLine < targetBottom)
                {
                    ClearRectangle ( sourceLeft, targetLine, sourceWidth, 1, character, color );
                    targetLine++;
                }
            }
            else
            {
                var targetLine = targetTop + sourceHeight - 1;
                for ( var srcLine = sourceBottom ; srcLine >= sourceTop ; srcLine--, targetLine-- )
                    CopyLine ( sourceLeft, srcLine, targetLeft, targetLine, sourceWidth );
                while ( targetLine >= targetTop )
                {
                    ClearRectangle ( sourceLeft, targetLine, sourceWidth, 1, character, color );
                    targetLine--;
                }
            }
        }
        
        // just copy single line
        private void CopyLine ( int sourceLeft, int surceLine, int targetLeft, int targetLine, int width )
        {
            var srcLine = surceLine * BufferWidth;
            var dstLine = targetLine * BufferWidth;
            if ( sourceLeft >= targetLeft )
            {
                var upto = sourceLeft + width;
                for (var x = sourceLeft; x < upto; x++)
                {
                    CharacterBuffer[ x + dstLine ] = CharacterBuffer[ x + srcLine ];
                    IsNegativeBuffer[ x + dstLine ] = IsNegativeBuffer[ x + srcLine ];
                    ForegroundBuffer[ x + dstLine ] = ForegroundBuffer[ x + srcLine ];
                }
            }
            else
            {
                var upto = sourceLeft;
                for (var x = sourceLeft + width - 1; x >= upto; x--)
                {
                    CharacterBuffer[ x + dstLine ] = CharacterBuffer[ x + srcLine ];
                    IsNegativeBuffer[ x + dstLine ] = IsNegativeBuffer[ x + srcLine ];
                    ForegroundBuffer[ x + dstLine ] = ForegroundBuffer[ x + srcLine ];
                }
            }
        }
        
        // ----------------------------------------------------------------------------------------------------
        // -- Rendering
        // ----------------------------------------------------------------------------------------------------

        public void Render ( Vector3 position, TargetResolution targetResolution )
        {
            if ( !IsVisible )
                return;

            GL.invertCulling = true;
            GL.PushMatrix ( );
            targetResolution.CalculateViewPortCoordinates();
            GL.LoadPixelMatrix(targetResolution.Left, targetResolution.Right, targetResolution.Bottom, targetResolution.Top);

            // Render background
            if ( IsBackgroundVisible )
            {
                backgroundMaterial.SetPass ( 0 );
                GL.Begin ( GL.QUADS );
                GL.Color ( theme.Background );
                GL.Vertex ( position + new Vector3 ( backgroundRectangle.min.x, backgroundRectangle.max.y, -1f ) );
                GL.Vertex ( position + new Vector3 ( backgroundRectangle.max.x, backgroundRectangle.max.y, -1f ) );
                GL.Vertex ( position + new Vector3 ( backgroundRectangle.max.x, backgroundRectangle.min.y, -1f ) );
                GL.Vertex ( position + new Vector3 ( backgroundRectangle.min.x, backgroundRectangle.min.y, -1f ) );
                GL.End ( );
            }
            // Render text buffer
            if ( IsTextVisible )
            {
                // render normal characters
                defaultMaterial.SetPass ( 0 );
                GL.Begin ( GL.QUADS );
                var negativeCharsCount = 0;
                CharacterInfo info = new CharacterInfo();
                for ( var addr = 0 ; addr < bufferSize ; addr++ )
                {
                    var c = CharacterBuffer[addr];
                    if (IsNegativeBuffer[addr])
                    {
                        negativeCharactersIndices[ negativeCharsCount++ ] = addr;
                        continue;
                    }
                    if ( c == ' ' )
                        continue;
                    if ( defaultFont.GetCharacterInfo ( c, out  info ) )
                    {
                        var charpos = position + charactersPositions[ addr ];
                        GL.Color ( ForegroundBuffer[ addr ]);
                        GL.MultiTexCoord ( 0, info.uvTopLeft );
                        GL.Vertex ( charpos + new Vector3 ( info.minX, info.maxY, 0 ) );
                        GL.MultiTexCoord ( 0, info.uvTopRight );
                        GL.Vertex ( charpos + new Vector3 ( info.maxX, info.maxY, 0 ) );
                        GL.MultiTexCoord ( 0, info.uvBottomRight );
                        GL.Vertex ( charpos + new Vector3 ( info.maxX, info.minY, 0 ) );
                        GL.MultiTexCoord ( 0, info.uvBottomLeft );
                        GL.Vertex ( charpos + new Vector3 ( info.minX, info.minY, 0 ) );
                    }
                }
                GL.End ( );
                
                // Render negative characters
                if (negativeCharsCount > 0)
                {
                    backgroundMaterial.SetPass ( 0 );
                    GL.Begin ( GL.QUADS );
                    GL.Color ( theme.SelectionColor );
                    var cursorSize = GetCursorSize ( );
                    var cursorOffset = new Vector3(0, CURSOR_OFFSET_Y,0);
                    for ( var i = 0 ; i < negativeCharsCount ; i++ )
                    {
                        var addr = negativeCharactersIndices[ i ];
                        var min = position + charactersPositions[ addr ] + cursorOffset;
                        var max = min + cursorSize;
                        GL.Vertex ( new Vector3 ( min.x, max.y, 0 ) );
                        GL.Vertex ( new Vector3 ( max.x, max.y, 0 ) );
                        GL.Vertex ( new Vector3 ( max.x, min.y, 0 ) );
                        GL.Vertex ( new Vector3 ( min.x, min.y, 0 ) );
                    }
                    GL.End ( );
                    
                    // Render inverted fonts                
                    defaultMaterial.SetPass ( 0 );
                    GL.Begin ( GL.QUADS );
                    for ( var i = 0 ; i < negativeCharsCount ; i++ )
                    {
                        var addr = negativeCharactersIndices[ i ];
                        var c = CharacterBuffer[addr];
                        if ( c == ' ' )
                            continue;
                        if ( defaultFont.GetCharacterInfo ( c, out info ) )
                        {
                            var charpos = position + charactersPositions[ addr ];
                            GL.Color ( ForegroundBuffer[ addr ] );
                            GL.MultiTexCoord ( 0, info.uvTopLeft );
                            GL.Vertex ( charpos + new Vector3 ( info.minX, info.maxY, 0 ) );
                            GL.MultiTexCoord ( 0, info.uvTopRight );
                            GL.Vertex ( charpos + new Vector3 ( info.maxX, info.maxY, 0 ) );
                            GL.MultiTexCoord ( 0, info.uvBottomRight );
                            GL.Vertex ( charpos + new Vector3 ( info.maxX, info.minY, 0 ) );
                            GL.MultiTexCoord ( 0, info.uvBottomLeft );
                            GL.Vertex ( charpos + new Vector3 ( info.minX, info.minY, 0 ) );
                        }
                    }
                    GL.End ( );
                }
            }
            // Render cursor
            if ( IsCursorVisible )
            {
                backgroundMaterial.SetPass ( 0 );
                var min = position + charactersPositions[cursor.X + cursor.Y * BufferWidth];
                min.y += CURSOR_OFFSET_Y;
                var max = min + GetCursorSize ( );
                GL.Begin ( GL.QUADS );
                GL.Color ( theme.CursorColor );
                GL.Vertex ( new Vector3 ( min.x, max.y, 0 ) );
                GL.Vertex ( new Vector3 ( max.x, max.y, 0 ) );
                GL.Vertex ( new Vector3 ( max.x, min.y, 0 ) );
                GL.Vertex ( new Vector3 ( min.x, min.y, 0 ) );
                GL.End ( );
            }
            GL.PopMatrix ( );
            GL.invertCulling = false;
        }

        private const float CURSOR_OFFSET_Y = -4;
        
        // -- Frame buffer -----------------------------------------------------------------------------------

        private char[] CharacterBuffer;               // Display buffer for every character of screen
        private Color[] ForegroundBuffer;             // Display buffer for every character of screen
        private bool[] IsNegativeBuffer;              // Display buffer for every character of screen
        private Vector3[] charactersPositions;        // PreCalculated position for each character
        private int[] negativeCharactersIndices;      // There will be indexes of negative characters

        // -- terminal fields --------------------------------------------------------------------------------

        public bool IsVisible;
        public bool IsBackgroundVisible;
        public bool IsCursorVisible;
        public bool IsTextVisible = true;
        public bool IsNegative;
        
        private readonly DisplayCursor cursor;

        private int bufferSize;
        private readonly Font defaultFont;
        private readonly Material defaultMaterial;
        private readonly Material backgroundMaterial;
        private readonly float lineHeight;
        private readonly float charAdvance;
        private Vector2 textPosition;
        private float textWidth;
        private float textHeight;
        private Rect backgroundRectangle;
        private float backgroundWidth;
        private float backgroundHeight;
        private readonly Vector2 backgroundPads = new Vector2 ( 5, 5 );
        private readonly Xresources theme;
        private Color foregroundColor = Color.white;

        // -- Read resources ----------------------------------------------------------------------------------

        private static Font ReadFont ( string fontPath )
        {
            var font = Resources.Load ( fontPath, typeof ( Font ) ) as Font;
            if ( font == null )
                Debug.LogErrorFormat ( "Font is not exists: '{0}'", fontPath );
            return font;
        }

        private static Material ReadMaterial ( string materialPath )
        {
            var material = Resources.Load ( materialPath, typeof ( Material ) ) as Material;
            if ( material == null )
                Debug.LogErrorFormat ( "Material is not exists: '{0}'", materialPath );
            return material;
        }
    }
}

