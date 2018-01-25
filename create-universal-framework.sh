#!/bin/sh
  PROJECT_ROOT=./MixpanelStaticLibrary
  PROJECT_NAME=Mixpanel
  PROJECT=${PROJECT_ROOT}/Pods/Pods.xcodeproj
  CONFIGURATION=Release
  BUILD_DIR=${PROJECT_ROOT}/build
  BUILD_ROOT=${PROJECT_ROOT}
  PLATFORM_NAME='iphoneos'
  UNIVERSAL_OUTPUTFOLDER=${BUILD_DIR}/${CONFIGURATION}-universal
  PROJECT_DIR=./MixpanelBindings/
   # make sure the output directory exists
   mkdir -p "${UNIVERSAL_OUTPUTFOLDER}"

   # Next, work out if we're in SIM or DEVICE
   if [ "false" == ${ALREADYINVOKED:-false} ]
   then

   export ALREADYINVOKED="true"

   xcodebuild -project ${PROJECT} -target ${PROJECT_NAME} ONLY_ACTIVE_ARCH=YES -configuration ${CONFIGURATION} -sdk iphoneos  BUILD_DIR="${BUILD_DIR}" BUILD_ROOT="${BUILD_ROOT}" clean build
   xcodebuild -project ${PROJECT} -target ${PROJECT_NAME} -configuration ${CONFIGURATION} -sdk iphonesimulator -arch x86_64 BUILD_DIR="${BUILD_DIR}" BUILD_ROOT="${BUILD_ROOT}" clean build

   # Step 2. Copy the framework structure (from iphoneos build) to the universal folder
   cp -R "${BUILD_DIR}/${CONFIGURATION}-iphoneos/${PROJECT_NAME}/${PROJECT_NAME}.framework" "${UNIVERSAL_OUTPUTFOLDER}/"

   # Step 3. Copy Swift modules from iphonesimulator build (if it exists) to the copied framework directory
   SIMULATOR_SWIFT_MODULES_DIR="${BUILD_DIR}/${CONFIGURATION}-iphonesimulator/${PROJECT_NAME}/${PROJECT_NAME}.framework/Modules/${PROJECT_NAME}.swiftmodule/."
   if [ -d "${SIMULATOR_SWIFT_MODULES_DIR}" ]; then
   cp -R "${SIMULATOR_SWIFT_MODULES_DIR}" "${UNIVERSAL_OUTPUTFOLDER}/${PROJECT_NAME}.framework/Modules/${PROJECT_NAME}.swiftmodule"
   fi

   # Step 4. Create universal binary file using lipo and place the combined executable in the copied framework directory
   lipo -create -output "${UNIVERSAL_OUTPUTFOLDER}/${PROJECT_NAME}.framework/${PROJECT_NAME}" "${BUILD_DIR}/${CONFIGURATION}-iphonesimulator/${PROJECT_NAME}/${PROJECT_NAME}.framework/${PROJECT_NAME}" "${BUILD_DIR}/${CONFIGURATION}-iphoneos/${PROJECT_NAME}/${PROJECT_NAME}.framework/${PROJECT_NAME}"

    # Step 5. Convenience step to copy the framework to the project's directory
    cp -R "${UNIVERSAL_OUTPUTFOLDER}/${PROJECT_NAME}.framework" "${PROJECT_DIR}"

    # Step 6. Convenience step to open the project's directory in Finder
    open "${PROJECT_DIR}"

    fi