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
            "NAME": "simulationSpeed",
            "LABEL": "Simulation speed",
            "TYPE": "float",
            "DEFAULT": 0.25,
            "MAX": 1,
            "MIN": 0
        },
        {
            "NAME": "trailSize",
            "LABEL": "Trail size",
            "TYPE": "float",
            "DEFAULT": 1.4,
            "MAX": 10,
            "MIN": 0
        },
        {
            "NAME": "trailDecay",
            "LABEL": "Trail trailDecay",
            "TYPE": "float",
            "DEFAULT": 0.15,
            "MAX": 10,
            "MIN": 0
        },
        {
            "NAME": "particleSpeed",
            "LABEL": "Particle speed",
            "TYPE": "float",
            "DEFAULT": 6,
            "MAX": 100,
            "MIN": 0
        },
        {
            "NAME": "particleSpeedRandomness",
            "LABEL": "Particle speed randomness",
            "TYPE": "float",
            "DEFAULT": 0.1,
            "MAX": 10,
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
            "NAME": "agentCloneDistance",
            "LABEL": "Agent clone distance",
            "TYPE": "float",
            "DEFAULT": 10,
            "MAX": 100,
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
        },
        {
            "NAME": "blurProportion",
            "LABEL": "Blur proportion",
            "TYPE": "float",
            "DEFAULT": 0.9,
            "MAX": 1,
            "MIN": 0
        }
    ],
    "ISFVSN": "2",
    "PASSES": [
        {
            "TARGET": "particles",
            "PERSISTENT": true
        },
        {
            "TARGET": "trails",
            "PERSISTENT": true
        },
        {
            "TARGET": "diffuseTrails",
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

// Hash functions (https://www.shadertoy.com/view/4djSRW)
float hash11(float p)
{
    p = fract(p * .1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

float hash12(vec2 p)
{
	vec3 p3 = fract(vec3(p.xyx) * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}


vec2 hash21(float p)
{
	vec3 p3 = fract(vec3(p) * vec3(.1031, .1030, .0973));
	p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xx + p3.yz) * p3.zy);
}

vec2 hash22(vec2 p)
{
	vec3 p3 = fract(vec3(p.xyx) * vec3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xx + p3.yz) * p3.zy);
}


// Functions
float gauss(vec2 x, float r)
{
    return exp(-pow(length(x) / r, 2.));
}

// Laplacian operator
vec4 Laplace(sampler2D ch, vec2 p)
{
    vec3 dx = vec3(-1, 0., 1);
    return texel(ch, p + dx.xy) +
           texel(ch, p + dx.yx) +
           texel(ch, p + dx.zy) +
           texel(ch, p + dx.yz) -
           4. * texel(ch, p);
}


//
// ShaderToy Buffer A
//

// Voronoi particle tracking
// Simulating the cells

// Loop the vector
vec2 loop_d(vec2 pos)
{
    vec2 halfSize = 0.5 * RENDERSIZE;
	return mod(pos + halfSize, RENDERSIZE) - halfSize;
}

// Loop the space
vec2 loop(vec2 pos)
{
	return mod(pos, RENDERSIZE);
}

void Check(inout vec4 particle, vec2 position, vec2 dx)
{
    vec4 neighborParticle = texel(particles, loop(position + dx));

    UNSCALE_PARTICLE(neighborParticle)

    // Check if the stored neighbouring particle is closer to this position.
    if (length(loop_d(neighborParticle.xy - position)) < length(loop_d(particle.xy - position))) {
        particle = neighborParticle; // Copy the particle info
    }
}

void CheckRadius(inout vec4 particle, vec2 position, float r)
{
    Check(particle, position, vec2(-r,  0));
    Check(particle, position, vec2( r,  0));
    Check(particle, position, vec2( 0, -r));
    Check(particle, position, vec2( 0,  r));
}

void main()
{
    vec2 position = gl_FragCoord.xy;

    if (PASSINDEX == 0) // ShaderToy Buffer A
    {
        // This pixel value
        vec4 particle = texel(particles, position);
        UNSCALE_PARTICLE(particle);

        // Check neighbours
        CheckRadius(particle, position, 1.);
        CheckRadius(particle, position, 2.);
        CheckRadius(particle, position, 3.);
        CheckRadius(particle, position, 4.);
        CheckRadius(particle, position, 5.);

        particle.xy = loop(particle.xy);

        // Cell cloning
        if (length(particle.xy - position) > agentCloneDistance) {
        	particle.xy += agentCloneFactor * (hash22(position) - 0.5);
        }

        // Sensors
        vec2 sleft = particle.xy + sdist * vec2(cos(particle.z + sangl), sin(particle.z + sangl));
        vec2 sright = particle.xy + sdist * vec2(cos(particle.z - sangl), sin(particle.z - sangl));

        float dangl = pixel(trails, sleft).x - pixel(trails, sright).x;
#ifndef VIDEOSYNC
#define tanh(x) (2. / (1. + exp(-2. * x)) - 1.)
#endif
        particle.z += simulationSpeed * sst * tanh(angleDifferenceFactor * dangl);

        vec2 pvel = particleSpeed * vec2(cos(particle.z), sin(particle.z)) + particleSpeedRandomness * (hash22(particle.xy + TIME) - 0.5);

        // Update the particle
        particle.xy += simulationSpeed * pvel;

        particle.xy = loop(particle.xy);

        if (FRAMEINDEX < 1 || restart) {
#ifndef VIDEOSYNC
#define round floor
#endif
            particle.xy = vec2(pdens * round(position.x / pdens), pdens * round(position.y / pdens));
            particle.zw = hash22(particle.xy) - 0.5;
        }

        SCALE_PARTICLE(particle);
        gl_FragColor = particle;
    }
    else if (PASSINDEX == 1) // ShaderToy Buffer B
    {
        vec4 trail = texel(trails, position);

        // Diffusion
        trail += simulationSpeed * Laplace(trails, position);

        vec4 particle = texel(particles, position);
        UNSCALE_PARTICLE(particle);

        float distr = gauss(position - particle.xy, trailSize);

        // Pheromone depositing
        trail += simulationSpeed * distr;

        // Pheromone decay
        trail -= simulationSpeed * trailDecay * trail;

        if (FRAMEINDEX < 1 || restart) {
            trail = vec4(0);
        }

        gl_FragColor = trail;
    }
    else if (PASSINDEX == 2) // ShaderToy Buffer C
    {
        gl_FragColor = blurProportion * texel(diffuseTrails, position) + (1. - blurProportion) * texel(trails, position);
        if (FRAMEINDEX < 1 || restart) {
            gl_FragColor = vec4(0);
        }
    }
    else if (PASSINDEX == 3) // ShaderToy Image
    {
        vec4 diffuseTrail = 2.5 * texel(diffuseTrails, position);
        gl_FragColor = vec4(sin(diffuseTrail.xyz * vec3(1, 1.2, 1.5)), 1.);
    }
}
