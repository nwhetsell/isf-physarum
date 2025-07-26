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
            "NAME": "mouse",
            "TYPE": "point2D",
            "DEFAULT": [1, 1],
            "MIN": [0, 0],
            "MAX": [1, 1]
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
            "LABEL": "Trail decay",
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
            "NAME": "particleMaxSearchRadius",
            "LABEL": "Particle max. search radius",
            "TYPE": "float",
            "DEFAULT": 5,
            "MAX": 10,
            "MIN": 1
        },
        {
            "NAME": "particleCloneFactor",
            "LABEL": "Particle clone factor",
            "TYPE": "float",
            "DEFAULT": 1,
            "MAX": 10,
            "MIN": 0
        },
        {
            "NAME": "particleCloneDistance",
            "LABEL": "Particle clone distance",
            "TYPE": "float",
            "DEFAULT": 10,
            "MAX": 100,
            "MIN": 0
        },
        {
            "NAME": "sensorDistance",
            "LABEL": "Sensor distance",
            "TYPE": "float",
            "DEFAULT": 10,
            "MAX": 100,
            "MIN": 0
        },
        {
            "NAME": "sensorStrength",
            "LABEL": "Sensor strength",
            "TYPE": "float",
            "DEFAULT": 10,
            "MAX": 100,
            "MIN": 0
        },
        {
            "NAME": "sensorAngle",
            "LABEL": "Sensor angle (radians)",
            "TYPE": "float",
            "DEFAULT": 0.3,
            "MAX": 6,
            "MIN": 0
        },
        {
            "NAME": "sensedDirectionFactor",
            "LABEL": "Sensed direction factor",
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

// These are also interesting defaults on the ISF website:
//   simulationSpeed: 0.19
//   trailSize: 6
//   trailDecay: 0.51
//   particleSpeed: 24.9
//   particleSpeedRandomness: 0
//   particleCloneFactor: 2.7
//   sensorDistance: 8.49
//   sensorStrength: 11.84
//   sensorAngle: 3.4
//   angleDifferenceFactor: 3

// In the ShaderToy shader, values less than 0 and greater than 1 are written to
// an image buffer. This seems to be impossible in an ISF shader; ISF shaders
// clamp image pixels to be between 0 and 1. Consequently, we must scale
// particle data to be between 0 and 1 when writing them to an image, and
// unscale particle data when reading from an image.
#define SCALE_PARTICLE(PARTICLE) PARTICLE.xy /= RENDERSIZE; PARTICLE.zw += 0.5;
#define UNSCALE_PARTICLE(PARTICLE) PARTICLE.xy *= RENDERSIZE; PARTICLE.zw -= 0.5;

//
// ShaderToy Common
//

// This should be an input variable, but the shader doesn’t initialize correctly
// unless this is a #define.
#define PARTICLE_DENSITY 2.

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


// This is the `loop` function in Buffer A of the original ShaderToy shader.
vec2 wrapToRenderSize(vec2 position)
{
	return mod(position, RENDERSIZE);
}

void main()
{
    vec2 position = gl_FragCoord.xy;

    if (PASSINDEX == 0) // ShaderToy Buffer A
    {
        float scaledSensorDistance = sensorDistance;
        float scaledSensorStrength = sensorStrength;
        if (length(mouse.xy) > 0.) {
           	scaledSensorDistance *= mouse.x;
      		scaledSensorStrength *= mouse.y;
        }

        // This pixel value
        vec4 particle = IMG_PIXEL(particles, position);
        UNSCALE_PARTICLE(particle);

        // Check neighbours
        vec2 halfSize = 0.5 * RENDERSIZE;
        for (float radius = 1.;
#ifdef VIDEOSYNC
             radius <= particleMaxSearchRadius;
#else
             radius <= 5.;
#endif
             radius += 1.) {

            // This would be *much* easier to do with an array initializer:
            //    vec2 positionOffsets[] = vec2[](vec2(-radius, 0), vec2(radius, 0), vec2(0, -radius), vec2(0, radius));
            // and the array length member function:
            //    https://www.khronos.org/opengl/wiki/Data_Type_(GLSL)#Arrays
            //    https://www.khronos.org/opengl/wiki/Data_Type_(GLSL)#Array_constructors
            const int positionOffsetCount = 4;
            vec2 positionOffsets[positionOffsetCount];
            positionOffsets[0] = vec2(-radius,  0);
            positionOffsets[1] = vec2( radius,  0);
            positionOffsets[2] = vec2( 0, -radius);
            positionOffsets[3] = vec2( 0,  radius);

            for (int i = 0; i < positionOffsetCount; i++) {
                vec4 neighbor = IMG_PIXEL(particles, wrapToRenderSize(position + positionOffsets[i]));
                UNSCALE_PARTICLE(neighbor);

                // Check if the stored neighbouring particle is closer to this position.
                float neighborDistance = length(wrapToRenderSize(neighbor.xy - position + halfSize) - halfSize);
                float particleDistance = length(wrapToRenderSize(particle.xy - position + halfSize) - halfSize);
                if (neighborDistance < particleDistance) {
                    particle = neighbor;
                }
            }
        }

        particle.xy = wrapToRenderSize(particle.xy);

        // Cell cloning
        if (length(particle.xy - position) > particleCloneDistance) {
        	particle.xy += particleCloneFactor * (hash22(position) - 0.5);
        }

        // Sensors
        vec2 sensorCounterclockwisePosition = particle.xy + sensorDistance * vec2(cos(particle.z + sensorAngle), sin(particle.z + sensorAngle));
        vec2 sensorClockwisePosition = particle.xy + sensorDistance * vec2(cos(particle.z - sensorAngle), sin(particle.z - sensorAngle));

        // It’s unclear whether IMG_NORM_PIXEL is doing any interpolation.
        float sensedDirection = IMG_NORM_PIXEL(trails, sensorCounterclockwisePosition / RENDERSIZE).x -
                                IMG_NORM_PIXEL(trails, sensorClockwisePosition / RENDERSIZE).x;
#ifndef VIDEOSYNC
#define tanh(x) (2. / (1. + exp(-2. * (x))) - 1.)
#endif
        particle.z += simulationSpeed * sensorStrength * tanh(sensedDirectionFactor * sensedDirection);

        vec2 particleVelocity = particleSpeed * vec2(cos(particle.z), sin(particle.z)) + particleSpeedRandomness * (hash22(particle.xy + TIME) - 0.5);

        // Update the particle
        particle.xy += simulationSpeed * particleVelocity;

        particle.xy = wrapToRenderSize(particle.xy);

        if (FRAMEINDEX < 1 || restart) {
#ifndef VIDEOSYNC
#define round(x) floor((x) + 0.5)
#endif
            particle.xy = vec2(PARTICLE_DENSITY * round(position.x / PARTICLE_DENSITY), PARTICLE_DENSITY * round(position.y / PARTICLE_DENSITY));
            particle.zw = hash22(particle.xy) - 0.5;
        }

        SCALE_PARTICLE(particle);
        gl_FragColor = particle;
    }
    else if (PASSINDEX == 1) // ShaderToy Buffer B
    {
        vec4 trail = IMG_PIXEL(trails, position);

        // Diffusion

        // This is the `Laplace` function in the Common tab of the original
        // ShaderToy shader. In the jit.gl.isf Max object (available with the
        // ISF package), it seems that IMG_PIXEL cannot be used outside the GLSL
        // main function, so inline the `Laplace` function here.
        vec3 dx = vec3(-1., 0., 1.);
        vec4 laplacian = IMG_PIXEL(trails, position + dx.xy) +
                         IMG_PIXEL(trails, position + dx.yx) +
                         IMG_PIXEL(trails, position + dx.zy) +
                         IMG_PIXEL(trails, position + dx.yz) -
                         4. * IMG_PIXEL(trails, position);

        trail += simulationSpeed * laplacian;

        vec4 particle = IMG_PIXEL(particles, position);
        UNSCALE_PARTICLE(particle);

        // Pheromone depositing
        trail += simulationSpeed * gauss(position - particle.xy, trailSize);

        // Pheromone decay
        trail -= simulationSpeed * trailDecay * trail;

        if (FRAMEINDEX < 1 || restart) {
            trail = vec4(0);
        }

        gl_FragColor = trail;
    }
    else if (PASSINDEX == 2) // ShaderToy Buffer C
    {
        gl_FragColor = blurProportion * IMG_PIXEL(diffuseTrails, position) + (1. - blurProportion) * IMG_PIXEL(trails, position);
        if (FRAMEINDEX < 1 || restart) {
            gl_FragColor = vec4(0);
        }
    }
    else if (PASSINDEX == 3) // ShaderToy Image
    {
        vec4 diffuseTrail = 2.5 * IMG_PIXEL(diffuseTrails, position);
        gl_FragColor = vec4(sin(diffuseTrail.xyz * vec3(1, 1.2, 1.5)), 1.);
    }
}
