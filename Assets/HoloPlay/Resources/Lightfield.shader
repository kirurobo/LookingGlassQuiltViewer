//Copyright 2017-2019 Looking Glass Factory Inc.
//All rights reserved.
//Unauthorized copying or distribution of this file, and the source code contained herein, is strictly prohibited.

Shader "Holoplay/Lightfield" {

	Properties {
		_MainTex ("Texture", 2D) = "white" {}
	}

	SubShader {
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			// default vars
			sampler2D _MainTex;
			float4 _MainTex_ST;
			// Holoplay vars
			float pitch;
			float slope;
			float center;
			float subpixelSize;
			float4 tile;
			float4 viewPortion;
			float4 aspect;
			float fringe;
			float verticalOffset; // just a dumb fix for macos in 2019.3
			
			// pass thru vert shader
			v2f vert (appdata v) {
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target {
				// just a dumb fix for macos in 2019.3
				i.uv.y += verticalOffset;

				// first handle aspect
				// note: recreated this using step functions because my mac didn't like the conditionals
				// if ((aspect.x > aspect.y && aspect.z < 0.5) || (aspect.x < aspect.y && aspect.z > 0.5))
				// 	viewUV.x *= aspect.x / aspect.y;
				// else 
				// 	viewUV.y *= aspect.y / aspect.x;
				float2 viewUV = i.uv;
				viewUV -= 0.5;
				float modx = saturate(
					step(aspect.y, aspect.x) * step(aspect.z, 0.5) +
					step(aspect.x, aspect.y) * step(0.5, aspect.z));
				viewUV.x = modx * viewUV.x * aspect.x / aspect.y +
						   (1.0 - modx) * viewUV.x;
				viewUV.y = modx * viewUV.y +
						   (1.0 - modx) * viewUV.y * aspect.y / aspect.x;
				viewUV += 0.5;
				clip(viewUV);
				clip(-viewUV + 1.0);

				// then sample quilt
				fixed4 col = fixed4(0,0,0,1);
				[unroll]
				for (int subpixel = 0; subpixel < 3; subpixel++) {
					// determine view for this subpixel based on pitch, slope, center
					float viewLerp = i.uv.x + subpixel * subpixelSize;
					viewLerp += i.uv.y * slope;
					viewLerp *= pitch;
					viewLerp -= center;
					// make sure it's positive and between 0-1
					viewLerp = 1.0 - fmod(viewLerp + ceil(abs(viewLerp)), 1.0);
					// translate to quilt coordinates
					float view = floor(viewLerp * tile.z); // multiply by total views
					float2 quiltCoords = float2(
						(fmod(view, tile.x) + viewUV.x) / tile.x,
						(floor(view / tile.x) + viewUV.y) / tile.y
					);
					quiltCoords *= viewPortion.xy;
					col[subpixel] = tex2D(_MainTex, quiltCoords)[subpixel];
				}

				// fringe
				// if fringe is negative, that means it's the odd pixels
				// this is so we don't have to store a separate bool for odd or even
				// so that's why we're adding ceil fringe
				// if it's positive it's like we're adding 1
				// so pixel 2 will become pixel 3 and 3 % 2 == 1
				// and so our fringe amount will be multiplied by 1 instead of 0,
				// so for a fringe of 0.2 would make the fringe amount 1 -> 0.8
				float yPixel = i.uv.y * _ScreenParams.y + ceil(fringe * 0.5);
				float fringeAmt = 1.0 - abs(fringe) * floor(fmod(yPixel, 2.0));
				col *= fringeAmt;

				return col;
			}
			ENDCG
		}
	}
}