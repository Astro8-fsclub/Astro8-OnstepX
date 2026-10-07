cmake_minimum_required(VERSION 3.13)

include(pico_sdk_import.cmake)

project(Rp2040HID C CXX ASM)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)

pico_sdk_init()

# 使用 Arduino rp2040 板卡包自带的预编译 picotool 生成 .uf2，
# 避免 SDK 尝试从 git 拉取并构建 picotool（本机构建机无 host 编译器）
if (NOT TARGET picotool)
    add_executable(picotool IMPORTED GLOBAL)
    set_target_properties(picotool PROPERTIES
        IMPORTED_LOCATION "C:/Users/DA ZHOU/AppData/Local/Arduino15/packages/rp2040/tools/pqt-picotool/5.0.0-9576866/picotool.exe")
endif()

add_executable(Rp2040HID
    main.c
    usb_descriptors.c
)

# 工程自己的 tusb_config.h 优先于 Pico SDK 自带默认配置
target_include_directories(Rp2040HID PRIVATE ${CMAKE_CURRENT_LIST_DIR})

target_link_libraries(Rp2040HID
    pico_stdlib
    tinyusb_device
    hardware_uart
    hardware_irq
    hardware_gpio
    pico_time
)

target_compile_definitions(Rp2040HID PRIVATE
    LIB_TINYUSB_HOST=0
    LIB_TINYUSB_DEVICE=1
)

# 不使用 stdio（UART0 全部留给 OnStep 通信）
pico_enable_stdio_uart(Rp2040HID 0)
pico_enable_stdio_usb(Rp2040HID 0)

# 自定义输出：dis / hex / bin / uf2
# （arduino 的 picotool 需 .elf 扩展名才能识别文件类型，故先复制出 .elf 再转 uf2）
add_custom_command(TARGET Rp2040HID POST_BUILD
    COMMAND ${CMAKE_OBJDUMP} -h $<TARGET_FILE:Rp2040HID> > ${CMAKE_CURRENT_BINARY_DIR}/Rp2040HID.dis
    COMMAND ${CMAKE_OBJDUMP} -d $<TARGET_FILE:Rp2040HID> >> ${CMAKE_CURRENT_BINARY_DIR}/Rp2040HID.dis
    COMMAND ${CMAKE_OBJCOPY} -Oihex $<TARGET_FILE:Rp2040HID> ${CMAKE_CURRENT_BINARY_DIR}/Rp2040HID.hex
    COMMAND ${CMAKE_OBJCOPY} -Obinary $<TARGET_FILE:Rp2040HID> ${CMAKE_CURRENT_BINARY_DIR}/Rp2040HID.bin
    COMMAND ${CMAKE_COMMAND} -E copy_if_different $<TARGET_FILE:Rp2040HID> ${CMAKE_CURRENT_BINARY_DIR}/Rp2040HID.elf
    COMMAND picotool uf2 convert --quiet ${CMAKE_CURRENT_BINARY_DIR}/Rp2040HID.elf ${CMAKE_CURRENT_BINARY_DIR}/Rp2040HID.uf2 --family rp2040
    COMMENT "Generate Rp2040HID .dis/.hex/.bin/.uf2"
    VERBATIM)
