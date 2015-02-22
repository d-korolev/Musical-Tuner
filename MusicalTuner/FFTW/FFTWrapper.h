#pragma once
#include "include/fftw3.h"

namespace FFTW
{
	// This is a simple value struct that packs memory the same way as an fftwf_complex
	public value struct Complex {
		float real, imag;
	};




    public ref class FFTWrapper sealed
    {
	private:
		// Internally managed buffers
		float * inputBuffer;
		fftwf_complex * outputBuffer;

		// Length of buffers
		unsigned int N;

		// The fftw plan we generate in the constructor, and the inverse plan
		fftwf_plan plan;
		fftwf_plan iplan;

    public:
        FFTWrapper( unsigned int N );
		virtual ~FFTWrapper();

		// Returns the length this FFTW calculates for us
		unsigned int getLength();

		// Calculate the complex-valued FFT, returning it as an array.  Note that this FFT will now zero-pad for you!
		Platform::Array<Complex>^ fft( const Platform::Array<float>^ input );

		// Calculate the inverse FFT, returning it as a real array.  Note that this method expects (N/2 + 1) samples!
		Platform::Array<float>^ ifft( const Platform::Array<Complex>^ input );

		// Returns the magnitude-only FFT.  Note that this FFT will now zero-pad for you!
		Platform::Array<float>^ fftMag( const Platform::Array<float>^ input );

		// Outputs the log-magnitude of the fft, same logical length as fft(), but doesn't have two elements per bin
		// Note that this FFT will now zero-pad for you!
		Platform::Array<float>^ fftLogMag( const Platform::Array<float>^ input );
    };
}