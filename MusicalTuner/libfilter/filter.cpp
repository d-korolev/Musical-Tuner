//#include "pch.h"
#include "filter.h"
#include <string.h>

using namespace libfilter;


Filter::Filter(const Array<float>^ impulseResponse) {
	this->N = impulseResponse->Length;
	this->impulseResponse = new float[this->N];

	// Copy impulseResponse in, backwards
	float * impulseResponseData = impulseResponse->Data;
	for (unsigned int i = 0; i<N; ++i) {
		this->impulseResponse[i] = impulseResponseData[(N - 1) - i];
	}

	this->prevData = new float[this->N];
	memset(this->prevData, 0, sizeof(float)*this->N);

	this->idx = 0;
}

Filter::~Filter() {
	delete[] this->impulseResponse;
	delete[] this->prevData;
}

Array<float>^ Filter::filter(const Array<float>^ data) {
	Array<float>^ result = ref new Array<float>(data->Length);
	// Just call filter(float) for each element of this float[]
	for (unsigned int i = 0; i<data->Length; ++i)
		result[i] = filter(data[i]);
	return result;
}

float Filter::filter(float data) {
	// Put data into prevData
	this->prevData[idx] = data;

	// Do dot product between prevData and impulseResponse:
	float result = 0.0f;
	for (unsigned int i = 0; i<N; ++i) {
		result += this->prevData[(i + idx) % N] * this->impulseResponse[i];
	}

	idx--;
	if (idx == -1)
		idx = N - 1;
	return result;
}
