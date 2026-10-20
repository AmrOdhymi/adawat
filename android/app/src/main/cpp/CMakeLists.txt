cmake_minimum_required(VERSION 3.22)
project(adawat_mp3 C)

# LAME comes from tools/fetch-lame.sh, not from this repository.
set(LAME ${CMAKE_CURRENT_SOURCE_DIR}/lame)
if(NOT EXISTS ${LAME}/libmp3lame/lame.c)
    message(FATAL_ERROR "LAME source is missing. Run android/tools/fetch-lame.sh first.")
endif()

file(GLOB LAME_SOURCES ${LAME}/libmp3lame/*.c)
list(FILTER LAME_SOURCES EXCLUDE REGEX ".*/mpglib_interface\\.c$")

add_library(adawat-mp3 SHARED mp3.c ${LAME_SOURCES})
target_include_directories(adawat-mp3 PRIVATE ${LAME}/include ${LAME}/libmp3lame)
target_compile_definitions(adawat-mp3 PRIVATE STDC_HEADERS HAVE_STDINT_H HAVE_LIMITS_H HAVE_STRING_H ieee754_float32_t=float)
target_compile_options(adawat-mp3 PRIVATE -O2 -w -fvisibility=hidden)
# 16 KB pages, which Google Play requires for native code.
target_link_options(adawat-mp3 PRIVATE "-Wl,-z,max-page-size=16384")
target_link_libraries(adawat-mp3 m log)
