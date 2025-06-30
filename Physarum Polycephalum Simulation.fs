/*{
    "CATEGORIES": [
        "Generator"
    ],
    "CREDIT": "Mykhailo Moroz <https://www.shadertoy.com/user/michael0884>",
    "DESCRIPTION": "Voronoi particle tracking to simulate dynamics of slime mold, converted from <https://www.shadertoy.com/view/tlKGDh>",
    "INPUTS": [
        {
            "NAME": "restart",
            "LABEL": "Restart",
            "TYPE": "event"
        },
        {
            "NAME": "dt",
            "LABEL": "Simulation speed",
            "TYPE": "float",
            "DEFAULT": 0.25,
            "MAX": 1,
            "MIN": 0
        },
        {
            "NAME": "prad",
            "LABEL": "Trail size",
            "TYPE": "float",
            "DEFAULT": 1.4,
            "MAX": 10,
            "MIN": 0
        },
        {
            "NAME": "decay",
            "LABEL": "Trail decay",
            "TYPE": "float",
            "DEFAULT": 0.15,
            "MAX": 10,
            "MIN": 0
        },
        {
            "NAME": "pspeed",
            "LABEL": "Agent speed",
            "TYPE": "float",
            "DEFAULT": 6,
            "MAX": 100,
            "MIN": 0
        },
        {
            "NAME": "agentCloneFactor",
            "LABEL": "Agent clone factor",
            "TYPE": "float",
            "DEFAULT": 1,
            "MAX": 10,
            "MIN": 0
        },
        {
            "NAME": "sdist",
            "LABEL": "Sensor distance",
            "TYPE": "float",
            "DEFAULT": 10,
            "MAX": 100,
            "MIN": 0
        },
        {
            "NAME": "sst",
            "LABEL": "Sensor strength",
            "TYPE": "float",
            "DEFAULT": 10,
            "MAX": 100,
            "MIN": 0
        },
        {
            "NAME": "sangl",
            "LABEL": "Sensor angle (radians)",
            "TYPE": "float",
            "DEFAULT": 0.3,
            "MAX": 6,
            "MIN": 0
        },
        {
            "NAME": "angleDifferenceFactor",
            "LABEL": "Sensor angle diff. factor",
            "TYPE": "float",
            "DEFAULT": 3,
            "MAX": 10,
            "MIN": 1
        }
    ],
    "ISFVSN": "2",
    "PASSES": [
        {
            "TARGET": "bufferA",
            "PERSISTENT": true
        },
        {
            "TARGET": "bufferB",
            "PERSISTENT": true
        },
        {
            "TARGET": "bufferC",
            "PERSISTENT": true
        },
        {

        }
    ]
}
*/

// In the ShaderToy shader, values less than 0 and greater than 1 are written to
// an image buffer. This seems to be impossible in an ISF shader; ISF shaders
// clamp image pixels to be between 0 and 1. Consequently, we must scale
// particle data to be between 0 and 1 when writing them to an image, and
// unscale particle data when reading from an image.
#define SCALE_PARTICLE(PARTICLE) PARTICLE.xy /= RENDERSIZE; PARTICLE.zw += 0.5;
#define UNSCALE_PARTICLE(PARTICLE) PARTICLE.xy *= RENDERSIZE; PARTICLE.zw -= 0.5;

// This should be an input variable, but the shader doesn’t initialize correctly
// unless this is a #define.
#define pdens 2.

// These probably shouldn’t be the same, but ISF shaders don’t seem to have any
// other options.
#define pixel(a, p) IMG_PIXEL(a, p)
#define texel(a, p) IMG_PIXEL(a, p)

//
// ShaderToy Common
//

//hash functions
//https://www.shadertoy.com/view/4djSRW
float hash11(float p)
{
    p = fract(p * .1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

float hash12(vec2 p)
{
	vec3 p3  = fract(vec3(p.xyx) * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}


vec2 hash21(float p)
{
	vec3 p3 = fract(vec3(p) * vec3(.1031, .1030, .0973));
	p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xx+p3.yz)*p3.zy);

}

vec2 hash22(vec2 p)
{
	vec3 p3 = fract(vec3(p.xyx) * vec3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx+33.33);
    return fract((p3.xx+p3.yz)*p3.zy);

}


//functions
float gauss(vec2 x, float r)
{
    return exp(-pow(length(x)/r,2.));
}

//Laplacian operator
vec4 Laplace(sampler2D ch, vec2 p)
{
    vec3 dx = vec3(-1,0.,1);
    return texel(ch, p+dx.xy)+texel(ch, p+dx.yx)+texel(ch, p+dx.zy)+texel(ch, p+dx.yz)-4.*texel(ch, p);
}


//
// ShaderToy Buffer A
//

//voronoi particle tracking
//simulating the cells

//loop the vector
vec2 loop_d(vec2 pos)
{
    vec2 halfSize = 0.5 * RENDERSIZE;
	return mod(pos + halfSize, RENDERSIZE) - halfSize;
}

//loop the space
vec2 loop(vec2 pos)
{
	return mod(pos, RENDERSIZE);
}


void Check(inout vec4 U, vec2 pos, vec2 dx)
{
    vec4 Unb = texel(bufferA, loop(pos+dx));

    UNSCALE_PARTICLE(Unb)

    //check if the stored neighbouring particle is closer to this position
    if(length(loop_d(Unb.xy - pos)) < length(loop_d(U.xy - pos)))
    {
        U = Unb; //copy the particle info
    }
}

void CheckRadius(inout vec4 U, vec2 pos, float r)
{
    Check(U, pos, vec2(-r,0));
    Check(U, pos, vec2(r,0));
    Check(U, pos, vec2(0,-r));
    Check(U, pos, vec2(0,r));
}

#define U gl_FragColor
#define Q gl_FragColor
#define p pos

void main()
{
    vec2 pos = gl_FragCoord.xy;

    if (PASSINDEX == 0) // ShaderToy Buffer A
    {
        //this pixel value
        U = texel(bufferA, pos);

        UNSCALE_PARTICLE(U)

        //check neighbours
        CheckRadius(U, pos, 1.);
        CheckRadius(U, pos, 2.);
        CheckRadius(U, pos, 3.);
        CheckRadius(U, pos, 4.);
        CheckRadius(U, pos, 5.);

        U.xy = loop(U.xy);

        //cell cloning
        if(length(U.xy - pos) > 10.)
        	U.xy += agentCloneFactor*(hash22(pos)-0.5);

        //sensors
        vec2 sleft = U.xy + sdist*vec2(cos(U.z+sangl), sin(U.z+sangl));
        vec2 sright = U.xy + sdist*vec2(cos(U.z-sangl), sin(U.z-sangl));

        float dangl = (pixel(bufferB, sleft).x - pixel(bufferB, sright).x);
#ifndef VIDEOSYNC
#define tanh(x) (2. / (1. + exp(-2. * x)) - 1.)
#endif
        U.z += dt*sst*tanh(angleDifferenceFactor*dangl);

        vec2 pvel = pspeed*vec2(cos(U.z), sin(U.z)) + 0.1*(hash22(U.xy+TIME)-0.5);;

        //update the particle
        U.xy += dt*pvel;

        U.xy = loop(U.xy);


        if(FRAMEINDEX < 1 || restart)
        {
#ifndef VIDEOSYNC
#define round floor
#endif
            U.xy = vec2(pdens*round(pos.x/pdens),pdens*round(pos.y/pdens));
            U.zw = hash22(U.xy) - 0.5;
        }

        SCALE_PARTICLE(U)
    }
    else if (PASSINDEX == 1) // ShaderToy Buffer B
    {
        Q = texel(bufferB, p);

        //diffusion equation
        Q += dt*Laplace(bufferB, p);

        vec4 particle = texel(bufferA, p);

        UNSCALE_PARTICLE(particle)

        float distr = gauss(p - particle.xy, prad);

        //pheromone depositing
        Q += dt*distr;

        //pheromone decay
        Q += -dt*decay*Q;

        if(FRAMEINDEX < 1 || restart) Q = vec4(0);
    }
    else if (PASSINDEX == 2) // ShaderToy Buffer C
    {
        Q = texel(bufferC, p);

        Q = 0.9*Q + 0.1*texel(bufferB, p);
        if(FRAMEINDEX < 1 || restart) Q =vec4(0);
    }
    else if (PASSINDEX == 3) // ShaderToy Image
    {
        vec4 pheromone = 2.5 * texel(bufferC, pos);
        gl_FragColor = vec4(sin(pheromone.xyz * vec3(1, 1.2, 1.5)), 1.);
    }
}
