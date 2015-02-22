#pragma once

// Include "using namespace Platform" so we can just use Array and not Platform::Array
using namespace Platform;
using namespace Windows::Foundation::Metadata;

namespace libfilter
{
    public ref class Filter sealed
    {
    public:
		// Create a filter with a given impulseResponse
        Filter( const Array<float>^ impulseResponse );

		// Clean everything up, releasing all internally created buffers, etc...
		virtual ~Filter();

		// Feed one new sample into the filter, return the new sample of output
		float filter( float data );

		// Convolve every sample in the input array, returning the array of new output samples
		// Ignore the [DefaultOverload] thing, it's just to silence a warning
		[DefaultOverload] Array<float>^ filter( const Array<float>^ data );
	private:
		float *impulseResponse, *prevData;
		unsigned int idx;
		unsigned int N;
	};
}
