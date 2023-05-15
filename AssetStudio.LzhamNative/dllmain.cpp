#include "lzham_core.h"
#include "lzham_decomp.h"

extern "C" LZHAM_DLL_EXPORT lzham_uint32 lzham_get_version(void)
{
    return LZHAM_DLL_VERSION;
}

extern "C" LZHAM_DLL_EXPORT lzham_decompress_state_ptr lzham_decompress_init(const lzham_decompress_params* pParams)
{
    return lzham::lzham_lib_decompress_init(pParams);
}

extern "C" LZHAM_DLL_EXPORT lzham_decompress_state_ptr lzham_decompress_reinit(
    lzham_decompress_state_ptr p, const lzham_decompress_params* pParams)
{
    return lzham::lzham_lib_decompress_reinit(p, pParams);
}

extern "C" LZHAM_DLL_EXPORT lzham_uint32 lzham_decompress_deinit(lzham_decompress_state_ptr p)
{
    return lzham::lzham_lib_decompress_deinit(p);
}

extern "C" LZHAM_DLL_EXPORT lzham_decompress_status_t lzham_decompress(
    lzham_decompress_state_ptr p,
    const lzham_uint8* pIn_buf, size_t* pIn_buf_size,
    lzham_uint8* pOut_buf, size_t* pOut_buf_size,
    lzham_bool no_more_input_bytes_flag)
{
    return lzham::lzham_lib_decompress(p, pIn_buf, pIn_buf_size, pOut_buf, pOut_buf_size, no_more_input_bytes_flag);
}

extern "C" LZHAM_DLL_EXPORT lzham_decompress_status_t lzham_decompress_memory(
    const lzham_decompress_params* pParams,
    lzham_uint8* pDst_buf, size_t* pDst_len,
    const lzham_uint8* pSrc_buf, size_t src_len,
    lzham_uint32* pAdler32)
{
    return lzham::lzham_lib_decompress_memory(pParams, pDst_buf, pDst_len, pSrc_buf, src_len, pAdler32);
}
