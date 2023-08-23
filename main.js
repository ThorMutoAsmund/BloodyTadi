require('./daw');

// Plan:

// Load 1-10 samples

// playback part of sample
// volume, speed, pan, fade in fade out
// run through effect
// mix


const koffi = require('koffi');

const lib = koffi.load('ModernAmplifier.dll');

// const lib = koffi.load('user32.dll');

// // Declare constants
// const MB_OK = 0x0;
// const MB_YESNO = 0x4;
// const MB_ICONQUESTION = 0x20;
// const MB_ICONINFORMATION = 0x40;
// const IDOK = 1;
// const IDYES = 6;
// const IDNO = 7;

// // Find functions
// const MessageBoxA = lib.stdcall('MessageBoxA', 'int', ['void *', 'str', 'str', 'uint']);
// const MessageBoxW = lib.stdcall('MessageBoxW', 'int', ['void *', 'str16', 'str16', 'uint']);

// let ret = MessageBoxA(null, 'Do you want another message box?', 'Koffi', MB_YESNO | MB_ICONQUESTION);
// if (ret == IDYES)
//     MessageBoxW(null, 'Hello World!', 'Koffi', MB_ICONINFORMATION);
    

return;

(async () => {
    randSeed(2);

    let root = newSong('My song');
    // set('volume', 0.2);
    let samples = await loadSamplesAsync(['AllYouNeedIs.wav','Falter.wav']);

    let kick = slice(samples[1], sec(2.03), beat(0.35));
    let snare = slice(samples[1], sec(2.31), beat(0.35));

    // play(snare);
    // return;
    // let y = slice(samples[1], sec(2), beat(1));

    let pattern = begin();
    set('volume', 0.5);
    add(kick, beat(0));
    add(snare, beat(1));
    add(kick, beat(2));
    add(kick, beat(2.5));
    add(snare, beat(3));
    end();

    let pattern2 = begin();
    set('volume', 0.5);
    add(kick, beat(0));
    add(snare, beat(1));
    add(kick, beat(1.75));
    add(kick, beat(2));
    add(kick, beat(2.5));
    add(snare, beat(3));
    end();

    let pattern3 = begin();
    set('volume', 0.5);
    add(kick, beat(0));
    add(kick, beat(1));
    add(kick, beat(2));
    add(kick, beat(3));
    add(snare, beat(0));
    add(snare, beat(0.25));
    add(snare, beat(0.75));
    add(snare, beat(1));
    add(snare, beat(1.25));
    add(snare, beat(1.75));
    add(snare, beat(2));
    add(snare, beat(2.25));
    add(snare, beat(2.75));
    add(snare, beat(3));
    add(snare, beat(3.25));
    add(snare, beat(3.75));
    end();

    let track1 = begin();
    add(pattern, beat(0));
    add(pattern2, beat(4));
    add(pattern, beat(8));
    add(pattern3, beat(12));
    end();

    let patternA1 = begin();
    set('bpm', 107);
    set('volume',0.2);
    let pos = 0;
    let intv = [0,0.5,0.5,0.25,0.5,0.25,0.5,0.5,0.25,0.25];
    for (let i=0; i<64; ++i) {
        add(slice(samples[0], 
            beat(Math.trunc(random()*240.0)), 
            beat(0.1 + random()*0.15)), 
            beat(pos, root));
        pos += intv[i % intv.length];
        if (pos >= 16) {
            break;
        }
    }
    // add(slice(samples[0], beat(1), beat(0.5)), beat(0));
    end();

    add(track1, beat(0));
    // addScope(track1, beat(16));
    add(patternA1, beat(0));
    
    play();
})();

