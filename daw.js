const fs = require('fs');
const wav = require('wav');
const Speaker = require('speaker');
const { Readable, Writable } = require('stream');

function Scope(template) {
    this.volume = 1.0;
    this.parts = [];
    this.bpm = template.bpm;
}

const fixedFormat = {
    audioFormat: 1,
    endianness: 'LE',
    channels: 2,
    sampleRate: 44100,
    byteRate: 176400,
    blockAlign: 4,
    bitDepth: 16,
    signed: true
};

const MAX_FLOAT = 100000.0;
const bytesPerSample = 4;
const bytesPerChannel = 2;
const secToSamples = 4 * 44100;
const stdBPM = 120;
const sampleMin = -32768;
const sampleMax = 32767;
const sampleMask = -4;

let root = { };
let current = { };
let stack = [ ];
let rand = mulberry32(0);

function checkFormat(format) {
    return isDeepEqual(format, fixedFormat);
}

function isDeepEqual(object1, object2) {
    const objKeys1 = Object.keys(object1);
    const objKeys2 = Object.keys(object2);
  
    if (objKeys1.length !== objKeys2.length) {
        return false;
    }
  
    for (let key of objKeys1) {
        const value1 = object1[key];
        const value2 = object2[key];
  
        const isObjects = isObject(value1) && isObject(value2);
  
        if ((isObjects && !isDeepEqual(value1, value2)) ||
            (!isObjects && value1 !== value2)) {
            return false;
        }
    }
    return true;
}

function isObject(object) {
    return object != null && typeof object === "object";
}

function loadSamplesAsync(samples) {
    let promises = [];

    samples.forEach(sample => {
        let promise = new Promise((resolve, reject) => {
            let reader = new wav.Reader();
            let theFormat;
            let buffers = [];
            const firstBuf = new Writable();
            firstBuf._write = (chunk, encoding, done) => {
                buffers.push(chunk);
                done();
            };

            reader.on('format', format => {
                reader.pipe(firstBuf);
                if (!checkFormat(format)) {
                    reject();
                }
            }).on('end' , function() {
                let finalBuffer = Buffer.concat(buffers);
                resolve(finalBuffer);
            });

            let file = fs.createReadStream(sample);
            file.pipe(reader);
        });
        promises.push(promise);
    });
    
    return Promise.all(promises);
}

function scopeToBuf(scope) {
    if (scope.parts.length == 0) {
        let output = Buffer.alloc(0);
        return output;
    }

    let start = scope.parts.reduce((a, c) => Math.min(a, c.start), MAX_FLOAT);
    let end = scope.parts.reduce((a, c) => {
        if (typeof c.buf === 'undefined') {
            c.buf = scopeToBuf(c.scope);
        }
        return Math.max(a, c.start + c.buf.length);
    }, 0);
    let len = end - start;

    let output = Buffer.alloc(len);
    output.fill(0);
    return scope.parts.reduce((a, c) => {
        mixIn(scope, c.buf, 0, a, c.start - start, c.buf.length);
        return a;
    }, output);
}

function limitSample(value) {
    return Math.min(Math.max(value, sampleMin), sampleMax);
}

function mixIn(scope, src, srcFrom, dest, destFrom, len) {
    for (let i = 0; i < len; i += bytesPerSample) {
        let l = limitSample(dest.readInt16LE(destFrom + i) + src.readInt16LE(srcFrom + i) * scope.volume);
        let r = limitSample(dest.readInt16LE(destFrom + i + bytesPerChannel) + src.readInt16LE(srcFrom + i + bytesPerChannel) * scope.volume);
        dest.writeInt16LE(l, destFrom + i);
        dest.writeInt16LE(r, destFrom + i + bytesPerChannel);
    }
}

function play(buffer) {
    if (typeof buffer === 'undefined') {
        console.log('Playing "' + root.title + '"');
        buffer = scopeToBuf(stack[0]);
    }
    else {
        console.log("Playing sample");
    }

    Readable.from(buffer).pipe(new Speaker(fixedFormat));
}

function sec(s) {
    return Math.trunc(secToSamples * s) & sampleMask;
}

function beat(b, scope) {
    if (typeof scope === 'undefined') {
        scope = current;
    }
    return Math.trunc(60 * secToSamples * b / scope.bpm) & sampleMask;
}

function slice(buf, start, len) {
    return buf.slice(start, start + len);
}

function repeat(buf, n) {
    var accum = Array(Math.max(0, n));
    for (var i = 0; i < n; i++) accum[i] = buf;
    return Buffer.concat(accum);
}

function beginScope() {    
    current = new Scope(current);
    stack.push(current);
    return current;
}

function endScope() {
    if (stack.length < 2) {
        console.log("Stack underflow");

        return current;
    }

    stack.pop();
    current = stack[stack.length - 1];
    
    return current;
}

function add(obj, start) {
    switch(obj.constructor.name)
    {
        case 'Buffer': current.parts.push({buf: obj, start}); break;
        case 'Scope': current.parts.push({scope: obj, start}); break;
    }    
}

function set(key, value) {
    current[key] = value;
}

function newSong(title) {
    current = new Scope({ bpm: stdBPM});
    current.title = title;

    root = current;

    stack = [ current ];
    return current;
}

function randSeed(seed) {
    rand = mulberry32(seed);
}

function mulberry32(a) {
    return function() {
      var t = a += 0x6D2B79F5;
      t = Math.imul(t ^ t >>> 15, t | 1);
      t ^= t + Math.imul(t ^ t >>> 7, t | 61);
      return ((t ^ t >>> 14) >>> 0) / 4294967296;
    }
}

function random() {
    return rand();
}

global.newSong = newSong;
global.loadSamplesAsync = loadSamplesAsync;
global.fixedFormat = fixedFormat;
global.play = play;
global.sec = sec;
global.beat = beat;
global.slice = slice;
global.repeat = repeat;
global.begin = beginScope;
global.end = endScope;
global.add = add;
global.set = set;
global.randSeed = randSeed;
global.random = random;